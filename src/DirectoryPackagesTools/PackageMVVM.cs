using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.DOM;
using DirectoryPackagesTools.Utils;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// MVVM view over <see cref="IPackageReferenceVersion"/>
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Name} {Version}")]
    public partial class PackageMVVM : BaseMVVM
    {
        #region lifecycle
        internal PackageMVVM(IPackageReferenceVersion local, NuGetPackageVersionInfo currver, NuGetClient client, Action updateAllVersions)
        {
            System.Diagnostics.Debug.Assert(local.PackageId == currver.Parent.Id);
            System.Diagnostics.Debug.Assert(local.Version.MinVersion == currver.Version);
            _LocalReference = local;
            _CurrentVersion = currver;

            _UpdateAllPackagesVersions = updateAllVersions;

            _Client = client;

            _RawAvailableVersions = currver
                .Parent
                .GetVersions()
                .Where(item => _ShowOutOfOrderVersion(currver.Parent.Id, item))
                .OrderByDescending(item => item)
                .ToArray();            

            var hidePrereleases = PackageClassifier.ShouldHidePrereleases(Metadata);
            if (local.Version.MinVersion.IsPrerelease) hidePrereleases = false;
            _AvailableVersions = new Lazy<IReadOnlyList<NUGETVERSION>>(() => _CreateVersionsView(_RawAvailableVersions, !hidePrereleases));

            AllDeprecated = currver.Parent.AllDeprecated;            

            NewestRelease = _GetNewestVersionAvailable(false);
            NewestPrerelease = _GetNewestVersionAvailable(true);            

            _ApplyPackageInfo(currver);            

            // remove preprelease from stable packages            
            if (PackageClassifier.IsUnitTestPackage(Metadata)) NewestPrerelease = null;
        }        

        private static IReadOnlyList<NUGETVERSION> _CreateVersionsView(IReadOnlyList<NUGETVERSION> versions, bool showPrerelease)
        {
            // if all are pre-releases, don't hide.
            if (versions.All(item => item.IsPrerelease)) showPrerelease = true;                 

            if (!showPrerelease) // hide prereleases
            {
                versions = versions
                    .Where(item => !item.IsPrerelease)
                    .OrderByDescending(item => item)
                    .ToArray();
            }

            return versions.ToList();            
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
            _CurrentVersion = pinfo;
        }

        #endregion

        #region data - project

        private Action _UpdateAllPackagesVersions;

        private readonly IPackageReferenceVersion _LocalReference;

        private readonly HashSet<XmlMSBuildProjectDOM> _ProjectsUsingThis = new HashSet<XmlMSBuildProjectDOM>();

        #endregion

        #region data - nuget

        private readonly NuGetClient _Client;

        private readonly IReadOnlyList<NUGETVERSION> _RawAvailableVersions;

        private readonly Lazy<IReadOnlyList<NUGETVERSION>> _AvailableVersions;

        private NuGetPackageVersionInfo _CurrentVersion;

        #endregion

        #region Properties - name        

        /// <summary>
        /// Gets the name of the package. Ex: "System.Numerics.Vectors"
        /// </summary>
        public string Name => _LocalReference.PackageId;

        /// <summary>
        /// Gets the first name of the package. Ex: 'Microsoft', 'System'
        /// </summary>
        public string Prefix => _LocalReference.PackagePrefix;

        public IReadOnlyList<NUGETVERSION> AvailableVersions => _AvailableVersions.Value;

        #endregion

        #region Properties - version

        public bool AllDeprecated { get; }
        public string DeprecationReason => _CurrentVersion?.DeprecationInfo?.Message + $"\r\nUse: {_CurrentVersion?.DeprecationInfo?.AlternatePackage}";


        public NUGETVERSION NewestRelease { get; }
        public NUGETVERSION NewestPrerelease { get; }

        public NUGETVERSION Version => _LocalReference.Version.MinVersion;

        public bool CanUpdate => !VersionIsUpToDate && !IsLocked;

        public bool VersionIsUpToDate
        {
            get
            {
                var v = _LocalReference.Version;
                if (NewestRelease != null && v.MinVersion == NewestRelease) return true;
                if (NewestPrerelease != null && v.MinVersion == NewestPrerelease) return true;
                return false;
            }
        }

        public bool IsLocked => _LocalReference.Version.HasUpperBound;        

        #endregion

        #region properties - metadata && dependent projects

        public IEnumerable<System.IO.FileInfo> DependantProjects => _ProjectsUsingThis.Select(item => item.File);

        public NUGETPACKMETADATA Metadata => _CurrentVersion.Metadata;

        public Task<string> FrameworksAsync => _GetFrameworksReportAsync();

        #endregion

        #region API

        private bool _SetVersionFeedback;

        /// <summary>
        /// Sets the new version for this package
        /// </summary>
        /// <param name="rver"></param>
        [RelayCommand]
        public void SetVersion(NUGETVERSION rver)
        {
            if (rver == null) return;

            if (_SetVersionFeedback) return;

            var lver = _LocalReference.Version;            

            if (lver.MinVersion == rver)
            {
                OnPropertyChanged(nameof(Version));
                return; // nothing to do
            }            

            var pinfo = _CurrentVersion.Parent;            

            _LocalReference.Version = new NUGETVERSIONRANGE(rver);
            _ApplyPackageInfo(pinfo[rver]);

            if (_UpdateAllPackagesVersions != null) _UpdateAllPackagesVersions.Invoke();
            else RaiseVersionChanged();
        }

        internal void RaiseVersionChanged()
        {
            if (_CurrentVersion.Version != _LocalReference.Version.MinVersion)
            {
                _CurrentVersion = _CurrentVersion.Parent[_LocalReference.Version.MinVersion];
            }

            _SetVersionFeedback = true;

            OnPropertyChanged(nameof(Version));
            OnPropertyChanged(nameof(VersionIsUpToDate));
            OnPropertyChanged(nameof(CanUpdate));
            OnPropertyChanged(nameof(IsLocked));
            OnPropertyChanged(nameof(Metadata));
            OnPropertyChanged(nameof(FrameworksAsync));

            _SetVersionFeedback = false;
        }

        internal string GetPackageCategory(PackageClassifier classifier)
        {
            if (AllDeprecated) return "⚠ Deprecated ⚠";            

            return classifier.GetPackageCategory(_CurrentVersion.Metadata);
        }

        private NUGETVERSION _GetNewestVersionAvailable(bool isPrerelease)
        {
            var vvv = AvailableVersions.ToList();

            var v = vvv
                .Where(item => item.IsPrerelease == isPrerelease)
                .OrderByDescending(item => item)
                .FirstOrDefault();

            return v;
        }

        public NUGETPACKIDENTITY GetCurrentIdentity()
        {
            return new NUGETPACKIDENTITY(Name, Version);
        }

        /// <summary>
        /// Adds a project that depends on this package
        /// </summary>        
        internal void _AddProjectDependent(XmlMSBuildProjectDOM prj)
        {
            _ProjectsUsingThis.Add(prj);
        }

        private async Task<string> _GetFrameworksReportAsync()
        {
            #if !DEBUG
            try {
            #endif                

                var dependencies = await _CurrentVersion.GetDependenciesAsync();
                if (dependencies == null) return "Unknown";

                var fff = dependencies
                    .DependencyGroups
                    .Select(item => item.TargetFramework.GetShortFolderName().Replace("netstandard", "netstd"));

                var result = string.Join(" ", fff);

                return result;

            #if !DEBUG
            }
            catch(Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
            #endif
        }

        #endregion
    }
}
