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
            var project = new Project(csprojFile.FullName);

            return new ManifestFactory(project);
        }

        private ManifestFactory(Project project)
        {            
            _Project = project;
            ProjectPath = new System.IO.FileInfo(project.FullPath);
        }

        #endregion

        #region data

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

        public string RepositoryType => _GetValueOrEmpty("RepositoryType");
        public string RepositoryUrl => _GetValueOrEmpty("RepositoryUrl");        
        public bool PublishRepositoryUrl => _GetValueOrEmpty("PublishRepositoryUrl").ToUpperInvariant() == "TRUE";

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


        public System.IO.FileInfo FindIcon()
        {
            var iconName = _GetValueOrNull("PackageIcon")
                ?? _GetValueOrNull("ApplicationIcon");

            var packableItems = _Project
                .Items
                .Where(item => item.HasMetadata("PackagePath") || item.GetMetadataValue("Pack") == "true")
                .ToArray();

            var iconItem = packableItems.FirstOrDefault(item => item.EvaluatedInclude.Contains(iconName));

            if (iconItem == null) return null;

            var iconPath = ProjectPath.Directory.DefineFile(iconItem.EvaluatedInclude);

            return iconPath.Exists ? iconPath : null;
        }

        public ManifestMetadata CreateMetadata()
        {
            // in here, we feed a ManifestMetadata from the properties of a csproj file.
            // somewhere in the code of NuGet tool it must be doing the same thing.

            // https://github.com/NuGet/NuGet.Client/blob/8c972cdff5b1194d7c37384fca5816a33ffbe0c4/src/NuGet.Clients/NuGet.CommandLine/Commands/ProjectFactory.cs#L549

            // Create a ManifestMetadata object and populate it with .csproj properties
            var metadata = new ManifestMetadata();

            metadata.Id = this.Id;
            metadata.Version = GetPackageVersion();
            metadata.Authors = this.Authors;
            metadata.Owners = this.Owners;
            metadata.Description = this.Description;
            metadata.Copyright = this.Copyright;
            metadata.Tags = this.PackageTags;
            metadata.SetProjectUrl(this.PackageProjectUrl);
            metadata.Repository = CreateRepoMetadata();

            var lic = this.PackageLicenseExpression;

            if (!string.IsNullOrWhiteSpace(lic))
            {
                var expr = NuGetLicenseExpression.Parse(lic);
                metadata.LicenseMetadata = new LicenseMetadata(LicenseType.Expression, lic, expr, Array.Empty<string>(), new Version("1.0.0"));
            }            

            return metadata;
        }

        public RepositoryMetadata CreateRepoMetadata()
        {
            if (!PublishRepositoryUrl) return null;
            if (string.IsNullOrWhiteSpace(RepositoryType) || string.IsNullOrWhiteSpace(RepositoryUrl)) return null;

            // https://github.com/NuGet/NuGet.Client/blob/8c972cdff5b1194d7c37384fca5816a33ffbe0c4/src/NuGet.Core/NuGet.Build.Tasks.Pack/PackTaskLogic.cs#L179

            var md = new RepositoryMetadata();
            md.Type = RepositoryType;
            md.Url = RepositoryUrl;

            return md;
        }

        #endregion
    }
}
