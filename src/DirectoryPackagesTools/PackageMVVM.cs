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

            _Metadata = pinfo.Metadata;
            _Dependencies = pinfo.Dependencies;

            _AvailableVersions = pinfo
                .GetVersions()
                .OrderByDescending(item => item)
                .ToArray();

            var hidePrereleases = _HidePrereleases(local.PackageId, pinfo.Metadata);

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

        private static bool _HidePrereleases(string name, NUGETPACKMETADATA meta)
        {
            if (name.StartsWith("System.")) return true;
            if (name.StartsWith("Xamarin.")) return true;
            if (name.StartsWith("Microsoft.")) return true;
            if (name.StartsWith("Prism.")) return true;
            if (name == "Google.Protobuf") return true;
            if (name == "SkiaSharp") return true;
            if (name == "MathNet.Numerics") return true;
            if (PackageClassifier.IsUnitTestPackage(meta)) return true;
            return false;
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

        public IReadOnlyList<NUGETVERSIONRANGE> AvailableVersions { get; }
        public NUGETVERSIONRANGE NewestRelease { get; }
        public NUGETVERSIONRANGE NewestPrerelease { get; }
        public NUGETVERSIONRANGE Version
        {
            get { return _LocalReference.Version; }
            set
            {
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

        public string Frameworks => string.Join(" ", _Dependencies.DependencyGroups.Select(item => item.TargetFramework.GetShortFolderName().Replace("netstandard","netstd")));

        #endregion

        #region API

        internal string GetPackageCategory(PackageClassifier classifier)
        {
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
