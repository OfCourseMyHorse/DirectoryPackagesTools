using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.DOM;

using NuGet.Packaging.Core;

using NUGETVERSION = NuGet.Versioning.NuGetVersion;
using NUGETDEPENDENCYINFO = NuGet.Protocol.Core.Types.FindPackageByIdDependencyInfo;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// MVVM view over <see cref="XmlPackageReferenceVersion"/>
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Name} {Version}")]
    public class PackageMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle
        internal PackageMVVM(XmlPackageReferenceVersion local, string source, NuGetPackageInfo pinfo)
        {
            _LocalReference = local;
            _AvailableVersions = pinfo.GetVersions();
            _Source = source;

            ApplyVersionCmd = new Prism.Commands.DelegateCommand<string>(ver => this.Version = ver);
        }

        #endregion

        #region data

        private readonly XmlPackageReferenceVersion _LocalReference;
        private readonly string _Source;
        private readonly IReadOnlyList<NUGETVERSION> _AvailableVersions;

        private readonly HashSet<XmlProjectDOM> _ProjectsUsingThis = new HashSet<XmlProjectDOM>();

        private NUGETDEPENDENCYINFO _Dependencies;

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

        public bool IsUser => !IsSystem && !IsTest;

        public bool IsSystem => !IsTest && (Constants.SystemPackages.Contains(Name) || Constants.SystemPrefixes.Any(p => Name.StartsWith(p + ".")));

        public bool IsTest => Constants.TestPackages.Contains(Name) || Constants.TestPrefixes.Any(p => Name.StartsWith(p + "."));        

        public IEnumerable<System.IO.FileInfo> DependantProjects => _ProjectsUsingThis.Select(item => item.File);

        public NUGETDEPENDENCYINFO DependencyInfo => _Dependencies;        

        #endregion

        #region Properties - version

        public string Version
        {
            get { return _LocalReference.Version; }
            set
            {
                _LocalReference.Version = value;
                RaisePropertyChanged(nameof(Version));
                RaisePropertyChanged(nameof(VersionIsUpToDate));
                RaisePropertyChanged(nameof(NeedsUpdate));
                RaisePropertyChanged(nameof(ExistingVersion));

                _Dependencies = null;
                RaisePropertyChanged(nameof(DependencyInfo));
            }
        }

        public NUGETVERSION ExistingVersion => _AvailableVersions.FirstOrDefault(item => item.ToNormalizedString() == this.Version);

        public IEnumerable<string> AvailableVersions => _AvailableVersions.Select(item => item.ToNormalizedString()).Reverse().ToList();

        public string NewestRelease => _AvailableVersions.Where(item => !item.IsPrerelease).OrderBy(item => item).LastOrDefault()?.ToString();

        public string NewestPrerelease => _AvailableVersions.Where(item => item.IsPrerelease).OrderBy(item => item).LastOrDefault()?.ToString();        

        public bool VersionIsUpToDate => Version == AvailableVersions.FirstOrDefault();

        public bool HasVersionRange => _LocalReference.HasVersionRange;

        public bool NeedsUpdate => !VersionIsUpToDate && !HasVersionRange;

        #endregion

        #region API

        public PackageIdentity GetCurrentIdentity()
        {
            var version = new NUGETVERSION(Version);

            return new PackageIdentity(Name, version);
        }

        internal void _AddDependent(XmlProjectDOM prj)
        {
            _ProjectsUsingThis.Add(prj);
        }

        internal async Task UpdateDependencyInfoAsync(NuGetClientContext context)
        {
            var pid = GetCurrentIdentity();

            _Dependencies = await context.GetDependencyInfoAsync(pid).ConfigureAwait(true);
            RaisePropertyChanged(nameof(DependencyInfo));
        }

        #endregion
    }
}
