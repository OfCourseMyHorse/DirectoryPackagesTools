using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Evaluation;

using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Licenses;
using NuGet.Versioning;


namespace SourceNugetPackageBuilder
{

    [System.Diagnostics.DebuggerDisplay("{ProjectPath.FullName,nq}")]
    class ManifestFactory
    {
        #region lifecycle

        public static ManifestFactory Create(System.IO.FileInfo csprojFile)
        {
            // Load the project
            var project = new ProjectEvaluator(csprojFile);

            var perFramework = project.TargetFrameworks.ToDictionary(item => item, item => new ProjectEvaluator(csprojFile, item));

            return new ManifestFactory(project, perFramework);
        }

        private ManifestFactory(ProjectEvaluator project, IReadOnlyDictionary<string, ProjectEvaluator> frameworkProject)
        {
            _Project = project;
            _FrameworkProject = frameworkProject;
        }

        #endregion

        #region data

        private readonly ProjectEvaluator _Project;
        private IReadOnlyDictionary<string,ProjectEvaluator> _FrameworkProject;

        #endregion

        #region properties

        public System.IO.FileInfo ProjectPath => _Project.ProjectPath;

        public IEnumerable<string> TargetFrameworks => _FrameworkProject.Keys;

        #endregion

        #region API

        public System.IO.FileInfo FindIcon() => _Project.FindIcon();

        public IEnumerable<System.IO.FileInfo> FindCompilableFiles(string targetFrameworkMoniker)
        {
            if (!_FrameworkProject.TryGetValue(targetFrameworkMoniker, out var prj)) throw new KeyNotFoundException(targetFrameworkMoniker);

            return prj.GetCompilableFiles();
        }

        public ManifestMetadata CreateMetadata()
        {
            // in here, we feed a ManifestMetadata from the properties of a csproj file.
            // somewhere in the code of NuGet tool it must be doing the same thing.

            // https://github.com/NuGet/NuGet.Client/blob/8c972cdff5b1194d7c37384fca5816a33ffbe0c4/src/NuGet.Clients/NuGet.CommandLine/Commands/ProjectFactory.cs#L549

            // Create a ManifestMetadata object and populate it with .csproj properties
            var metadata = new ManifestMetadata();

            metadata.Id = _Project.Id;
            metadata.Version = _Project.GetPackageVersion();
            metadata.Authors = _Project.Authors;
            metadata.Owners = _Project.Owners;
            metadata.Description = _Project.Description;
            metadata.Copyright = _Project.Copyright;
            metadata.Tags = _Project.PackageTags;

            if (Uri.TryCreate(_Project.PackageProjectUrl, UriKind.Absolute, out var _)) metadata.SetProjectUrl(_Project.PackageProjectUrl);

            metadata.Repository = CreateRepoMetadata();

            var lic = _Project.PackageLicenseExpression;

            if (!string.IsNullOrWhiteSpace(lic))
            {
                var expr = NuGetLicenseExpression.Parse(lic);
                metadata.LicenseMetadata = new LicenseMetadata(LicenseType.Expression, lic, expr, Array.Empty<string>(), new Version("1.0.0"));
            }            

            return metadata;
        }

        public RepositoryMetadata CreateRepoMetadata()
        {
            if (!_Project.PublishRepositoryUrl) return null;
            if (string.IsNullOrWhiteSpace(_Project.RepositoryType) || string.IsNullOrWhiteSpace(_Project.RepositoryUrl)) return null;

            // https://github.com/NuGet/NuGet.Client/blob/8c972cdff5b1194d7c37384fca5816a33ffbe0c4/src/NuGet.Core/NuGet.Build.Tasks.Pack/PackTaskLogic.cs#L179

            var md = new RepositoryMetadata();
            md.Type = _Project.RepositoryType;
            md.Url = _Project.RepositoryUrl;

            return md;
        }

        #endregion
    }
}
