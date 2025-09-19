using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DirectoryPackagesTools.DOM;
using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.Utils;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// MVVM view over <see cref="IPackageReferenceVersion"/>
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Name} {Version}")]
    public partial class PackageMVVM : BaseMVVM
    {
        #region lifecycle
        internal PackageMVVM(IPackageReferenceVersion local, NuGetPackageVersionInfo pinfo, NuGetClient client)
        {
            _Client = client;

            _LocalReference = local;

            _RawAvailableVersions = pinfo
                .Parent
                .GetVersions()
                .Where(item => _ShowOutOfOrderVersion(pinfo.Parent.Id, item))
                .OrderByDescending(item => item)
                .ToArray();

            AllDeprecated = pinfo.Parent.AllDeprecated;            

            _DefineVisibleAvailableVersions(local.Version.MinVersion.IsPrerelease);

            NewestRelease = _GetNewestVersionAvailable(false);
            NewestPrerelease = _GetNewestVersionAvailable(true);

            // remove preprelease from stable packages            
            if (PackageClassifier.IsUnitTestPackage(_Metadata)) NewestPrerelease = null;

            _ApplyPackageInfo(pinfo);            
        }        

        private void _DefineVisibleAvailableVersions(bool showPrerelease)
        {
            var hidePrereleases = PackageClassifier.ShouldHidePrereleases(_Metadata);

            var versions = _RawAvailableVersions;


            // if all are pre-releases, don't hide.
            if (versions.All(item => item.IsPrerelease)) hidePrereleases = false;

            // if current version is a pre-release, don't hide.
            if (showPrerelease) hidePrereleases = false;            

            if (hidePrereleases)
            {
                versions = versions
                    .Where(item => !item.IsPrerelease)
                    .OrderByDescending(item => item)
                    .ToArray();
            }

            AvailableVersions = versions.Select(item => new NUGETVERSIONRANGE(item)).ToList();            
        }

        /// <summary>
        /// This is used to hide out of order versions
        /// </summary>
        /// <returns>true if the version is to be shown</returns>
        private static bool _ShowOutOfOrderVersion(string name, NUGETVERSION ver)
        {
            if (name == "System.ComponentModel.Composition")
            {                
                if (ver.OriginalVersion.StartsWith("2010.2.11")) return false;
            }

            return true;
        }

        private void _ApplyPackageInfo(NuGetPackageVersionInfo pinfo)
        {
            // packages stored in a local source directory may report metadata as Null
            _Metadata = pinfo.Metadata;

            _Dependencies = pinfo.Dependencies;

            DeprecationReason = pinfo?.DeprecationInfo?.Message + $"\r\nUse: {pinfo?.DeprecationInfo?.AlternatePackage}";
        }

        #endregion

        #region data - project

        private readonly IPackageReferenceVersion _LocalReference;

        private readonly HashSet<XmlMSBuildProjectDOM> _ProjectsUsingThis = new HashSet<XmlMSBuildProjectDOM>();

        #endregion

        #region data - nuget

        private readonly NuGetClient _Client;

        private readonly IReadOnlyList<NUGETVERSION> _RawAvailableVersions;        

        private NUGETPACKMETADATA _Metadata;
        private NUGETPACKDEPENDENCIES _Dependencies;

        #endregion

        #region Properties - name        

        /// <summary>
        /// Gets the name of the package. Ex: "System.Numerics.Vectors"
        /// </summary>
        public string Name => _LocalReference.PackageId;

        /// <summary>
        /// Gets all the versions available for this package
        /// </summary>        
        [ObservableProperty]
        private IReadOnlyList<NUGETVERSIONRANGE> availableVersions;

        /// <summary>
        /// Gets the first name of the package. Ex: 'Microsoft', 'System'
        /// </summary>
        public string Prefix => _LocalReference.PackagePrefix;

        #endregion        

        #region Properties - version

        public bool AllDeprecated { get; }
        public string DeprecationReason { get; private set; }

        
        public NUGETVERSIONRANGE NewestRelease { get; }
        public NUGETVERSIONRANGE NewestPrerelease { get; }

        public NUGETVERSIONRANGE Version
        {
            get => _LocalReference.Version;
            private set
            {
                if (value == null) return;
                _LocalReference.Version = value;
                OnPropertyChanged(nameof(Version));
                OnPropertyChanged(nameof(VersionIsUpToDate));
                OnPropertyChanged(nameof(NeedsUpdate));
                OnPropertyChanged(nameof(Metadata));
                OnPropertyChanged(nameof(Frameworks));
            }
        }        

        public bool VersionIsUpToDate => Version.MinVersion == AvailableVersions.FirstOrDefault()?.MinVersion;
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

                var fff = _Dependencies
                    .DependencyGroups
                    .Select(item => item.TargetFramework.GetShortFolderName().Replace("netstandard", "netstd"));

                return string.Join(" ", fff);
            }
        }

        #endregion

        #region API

        /// <summary>
        /// Sets the new version for this package
        /// </summary>
        /// <param name="ver"></param>
        [RelayCommand]
        public async Task ApplyVersionAsync(NUGETVERSIONRANGE ver)
        {
            if (ver == null) return;

            var pinfo = new NuGetPackageInfo(_LocalReference.PackageId);

            await pinfo.UpdateAsync(_Client);

            _ApplyPackageInfo(pinfo[ver.MinVersion]);

            this.Version = ver;
        }

        internal string GetPackageCategory(PackageClassifier classifier)
        {
            if (AllDeprecated) return "⚠ Deprecated ⚠";            

            return classifier.GetPackageCategory(_Metadata);
        }

        private NUGETVERSIONRANGE _GetNewestVersionAvailable(bool isPrerelease)
        {
            var v = AvailableVersions
            .Where(item => item.MinVersion.IsPrerelease == isPrerelease)
            .OrderByDescending(item => item.MinVersion)
            .FirstOrDefault();

            return v == null ? null : new NUGETVERSIONRANGE(v.MinVersion);
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
