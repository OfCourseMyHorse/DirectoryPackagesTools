using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

using NuGet.Versioning;

namespace SourceNugetPackageBuilder
{
    [System.Diagnostics.DebuggerDisplay("{ProjectPath.FullName,nq}")]
    internal class ProjectEvaluator
    {
        #region lifecycle

        public static IReadOnlyCollection<System.IO.FileInfo> GetSolutionProjectFiles(System.IO.FileInfo solutionInfo)
        {
            var solution = SolutionFile.Parse(solutionInfo.FullName);            

            var prjs = new List<System.IO.FileInfo>();

            foreach (var slnItem in solution.ProjectsInOrder)
            {
                if (slnItem.ProjectType == SolutionProjectType.SolutionFolder) continue;
                if (slnItem.ProjectType == SolutionProjectType.SharedProject) continue;                

                var prj = new System.IO.FileInfo(slnItem.AbsolutePath);
                prjs.Add(prj);
            }

            return prjs;
        }

        public static IReadOnlyCollection<ProjectEvaluator> FromSolution(System.IO.FileInfo solutionInfo)
        {
            var solutionProjects = GetSolutionProjectFiles(solutionInfo);

            var projectCollection = new ProjectCollection();

            var prjs = new List<ProjectEvaluator>();

            foreach (var projectInSolution in solutionProjects)
            {
                var prj = new ProjectEvaluator(projectCollection, projectInSolution.FullName);
                prjs.Add(prj);
            }

            return prjs;
        }

        public ProjectEvaluator(System.IO.FileInfo finfo)
        {
            _TargetFramework = null;
            _Project = new Project(finfo.FullName);
            ProjectPath = new System.IO.FileInfo(_Project.FullPath);
        }

        private ProjectEvaluator(ProjectCollection projectCollection, string csprojPath)
        {
            _TargetFramework = null;
            _Project = projectCollection.LoadProject(csprojPath);
            ProjectPath = new System.IO.FileInfo(_Project.FullPath);
        }

        public ProjectEvaluator(System.IO.FileInfo finfo, string targetFrameworkMoniker)
        {
            var projectCollection = new ProjectCollection();
            projectCollection.SetGlobalProperty("TargetFramework", targetFrameworkMoniker);

            _TargetFramework = targetFrameworkMoniker;
            _Project = projectCollection.LoadProject(finfo.FullName);
            ProjectPath = new System.IO.FileInfo(_Project.FullPath);
        }        

        #endregion

        #region data

        private readonly string _TargetFramework;

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private readonly Project _Project;

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        public System.IO.FileInfo ProjectPath { get; }        

        #endregion

        #region properties

        /// <summary>
        /// Package ID
        /// </summary>
        public string Id
        {
            get
            {
                var id = _GetValueOrNull("PackageId");
                if (!string.IsNullOrWhiteSpace(id)) return id;

                // fallback to AssemblyName
                id = _GetValueOrNull("AssemblyName");
                if (!string.IsNullOrWhiteSpace(id)) return id;

                // fallback to Project name
                return System.IO.Path.GetFileNameWithoutExtension(_Project.FullPath);
            }
        }

        /// <summary>
        /// Target frameworks used by the project
        /// </summary>
        public string[] TargetFrameworks
        {
            get
            {
                var frameworks = _GetValueOrNull("TargetFrameworks");
                if (string.IsNullOrWhiteSpace(frameworks)) return [_GetValueOrNull("TargetFramework") ?? "netstandard2.0"];

                return frameworks.Split(';');
            }
        }

        public string[] Authors => _GetValueOrEmpty("Authors").Split(';');
        public string[] Owners => _GetValueOrEmpty("Owners").Split(';');
        public string Copyright => _GetValueOrNull("Copyright");
        public string PackageTags => _GetValueOrNull("PackageTags");
        public string Description => _GetValueOrNull("Description") ?? "Package Description";
        public string PackageLicenseExpression => _GetValueOrEmpty("PackageLicenseExpression");
        public string PackageProjectUrl => _GetValueOrEmpty("PackageProjectUrl");

        public string RepositoryType => _GetValueOrNull("RepositoryType");
        public string RepositoryUrl => _GetValueOrNull("RepositoryUrl");
        public bool PublishRepositoryUrl => _GetValueOrEmpty("PublishRepositoryUrl")?.ToUpperInvariant() == "TRUE";

        public bool IsPackableAsSources => _GetValueOrEmpty("IsPackableAsSources")?.ToUpperInvariant() == "TRUE";

        public bool PackAsInternalSources => _GetValueOrEmpty("PackAsInternalSources")?.ToUpperInvariant() == "TRUE";

        #endregion

        #region API

        private string _GetValueOrEmpty(string name)
        {
            return _Project.GetPropertyValue(name) ?? string.Empty;
        }

        private string _GetValueOrNull(string name)
        {
            var value = _Project.GetPropertyValue(name);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public NuGetVersion GetPackageVersion()
        {
            // https://andrewlock.net/version-vs-versionsuffix-vs-packageversion-what-do-they-all-mean/

            var pv = _GetValueOrNull("PackageVersion") ?? "1.0.0";
            return NuGetVersion.Parse(pv);
        }

        public IEnumerable<System.IO.FileInfo> GetCompilableFiles()
        {
            if (_TargetFramework == null) throw new InvalidOperationException("compilable files must be evaluted on a project loaded for a specific target framework");            

            return _Project
                .GetItems("Compile")
                .Select(_GetItemPath)
                .Where(item => item != null)
                .ToList();
        }

        public System.IO.FileInfo FindIcon()
        {
            // find the icon name
            var iconName = _GetValueOrNull("PackageIcon");

            if (string.IsNullOrWhiteSpace(iconName)) return null;

            // find the item with the given name            

            var iconItem = _Project
                .Items
                .Where(item => item.EvaluatedInclude.EndsWith(iconName, StringComparison.OrdinalIgnoreCase))
                .Where(item => item.HasMetadata("PackagePath"))
                .FirstOrDefault();

            if (iconItem == null) return null;
            if (iconItem.GetMetadataValue("Pack") != "true") return null;            

            var iconPath = _GetItemPath(iconItem);

            return iconPath.Exists ? iconPath : null;
        }

        private System.IO.FileInfo _GetItemPath(ProjectItem item)
        {
            // item.EvaluatedInclude may be relative to current project, or absolute path (specially if it uses $MSBuildThisFileDirectory);

            if (item == null) return null;
            if (string.IsNullOrWhiteSpace(item.EvaluatedInclude)) return null;
            if (System.IO.Path.IsPathRooted(item.EvaluatedInclude)) return new System.IO.FileInfo(item.EvaluatedInclude);
            return ProjectPath.Directory.DefineFile(item.EvaluatedInclude);
        }

        #endregion
    }
}
