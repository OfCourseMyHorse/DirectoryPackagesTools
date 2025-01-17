using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.DOM;

using NUGETVERSION = NuGet.Versioning.NuGetVersion;
using NUGETVERSIONRANGE = NuGet.Versioning.VersionRange;
using NUGETPACKIDENTITY = NuGet.Packaging.Core.PackageIdentity;
using NUGETPACKMETADATA = NuGet.Protocol.Core.Types.IPackageSearchMetadata;
using NUGETPACKDEPENDENCIES = NuGet.Protocol.Core.Types.FindPackageByIdDependencyInfo;
using NuGet.Protocol;


namespace DirectoryPackagesTools
{
    /// <summary>
    /// MVVM view over <see cref="IPackageReferenceVersion"/>
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Name} {Version}")]
    public class PackageMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle
        internal PackageMVVM(IPackageReferenceVersion local, NuGetPackageInfo pinfo)
        {
            _LocalReference = local;

            // packages stored in a local source directory may report metadata as Null
            _Metadata = pinfo.Metadata;            

            var hidePrereleases = PackageClassifier.HasHiddenPrereleases(_Metadata);

            _Dependencies = pinfo.Dependencies;

            AllDeprecated = pinfo.AllDeprecated;            

            _AvailableVersions = pinfo
                .GetVersions()
                .Where(item => _ShowVersion(pinfo.Id, item))
                .OrderByDescending(item => item)
                .ToArray();            

            // if all are pre-releases, don't hide.
            if (_AvailableVersions.All(item => item.IsPrerelease)) hidePrereleases = false;

            // if current version is a pre-release, don't hide.
            if (local.Version.MinVersion.IsPrerelease) hidePrereleases = false;

            if (hidePrereleases)
            {
                _AvailableVersions = _AvailableVersions
                    .Where(item => !item.IsPrerelease)
                    .OrderByDescending(item => item)
                    .ToArray();
            }
            
            AvailableVersions = _AvailableVersions.Select(item => new NUGETVERSIONRANGE(item)).ToList();
            NewestRelease = _GetNewestVersionAvailable(false);
            NewestPrerelease = _GetNewestVersionAvailable(true);            

            ApplyVersionCmd = new Prism.Commands.DelegateCommand<NUGETVERSIONRANGE>(ver => this.Version = ver);

            // remove preprelease from stable packages            
            if (PackageClassifier.IsUnitTestPackage(_Metadata)) NewestPrerelease = null;
        }

        /// <summary>
        /// This is used to hide out of order versions
        /// </summary>
        /// <returns>true if the version is to be shown</returns>
        private static bool _ShowVersion(string name, NUGETVERSION ver)
        {
            if (name == "System.ComponentModel.Composition")
            {                
                if (ver.OriginalVersion.StartsWith("2010.2.11")) return false;
            }

            return true;
        }        

        #endregion

        #region data        

        private readonly IPackageReferenceVersion _LocalReference;
        
        private readonly IReadOnlyList<NUGETVERSION> _AvailableVersions;

        private readonly HashSet<XmlMSBuildProjectDOM> _ProjectsUsingThis = new HashSet<XmlMSBuildProjectDOM>();

        private readonly NUGETPACKMETADATA _Metadata;
        private readonly NUGETPACKDEPENDENCIES _Dependencies;

        #endregion

        #region Properties - name

        public ICommand ApplyVersionCmd { get; }

        /// <summary>
        /// Gets the name of the package. Ex: "System.Numerics.Vectors"
        /// </summary>
        public string Name => _LocalReference.PackageId;

        /// <summary>
        /// Gets the first name of the package. Ex: 'Microsoft', 'System'
        /// </summary>
        public string Prefix => _LocalReference.PackagePrefix;                

        #endregion        

        #region Properties - version

        public bool AllDeprecated { get; }

        public IReadOnlyList<NUGETVERSIONRANGE> AvailableVersions { get; }
        public NUGETVERSIONRANGE NewestRelease { get; }
        public NUGETVERSIONRANGE NewestPrerelease { get; }
        public NUGETVERSIONRANGE Version
        {
            get => _LocalReference.Version;
            set
            {
                if (value == null) return;
                _LocalReference.Version = value;
                RaisePropertyChanged(nameof(Version));
                RaisePropertyChanged(nameof(VersionIsUpToDate));
                RaisePropertyChanged(nameof(NeedsUpdate));
            }
        }        

        public bool VersionIsUpToDate => Version.MinVersion == _AvailableVersions.FirstOrDefault();
        public bool NeedsUpdate => !VersionIsUpToDate && !Version.HasUpperBound;

        #endregion

        #region properties - metadata && dependent projects

        public IEnumerable<System.IO.FileInfo> DependantProjects => _ProjectsUsingThis.Select(item => item.File);

        public NUGETPACKMETADATA Metadata => _Metadata;

        public string Frameworks
        {
            get
            {
                if (_Dependencies == null) return "Unknown";
                return string.Join(" ", _Dependencies.DependencyGroups.Select(item => item.TargetFramework.GetShortFolderName().Replace("netstandard", "netstd")));
            }
        }

        #endregion

        #region API

        internal string GetPackageCategory(PackageClassifier classifier)
        {
            if (AllDeprecated) return "⚠ Deprecated ⚠";

            return classifier.GetPackageCategory(_Metadata);
        }

        private NUGETVERSIONRANGE _GetNewestVersionAvailable(bool isPrerelease)
        {
            var v = _AvailableVersions
            .Where(item => item.IsPrerelease == isPrerelease)
            .OrderByDescending(item => item)
            .FirstOrDefault();

            return v == null ? null : new NUGETVERSIONRANGE(v);
        }

        public NUGETPACKIDENTITY GetCurrentIdentity()
        {
            var version = Version.MinVersion;

            return new NUGETPACKIDENTITY(Name, version);
        }

        /// <summary>
        /// Adds a project that depends on this package
        /// </summary>        
        internal void _AddDependent(XmlMSBuildProjectDOM prj)
        {
            _ProjectsUsingThis.Add(prj);
        }        

        #endregion
    }
}
