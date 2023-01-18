using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DirectoryPackagesTools
{
    [System.Diagnostics.DebuggerDisplay("{Name} {Version}")]
    public class PackageMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle
        internal PackageMVVM(XmlPackageReferenceVersion local, string source, IReadOnlyList<NuGet.Versioning.NuGetVersion> versions)
        {
            _LocalReference = local;
            _AvailableVersions = versions;
            _Source = source;

            ApplyVersionCmd = new Prism.Commands.DelegateCommand<string>(ver => this.Version = ver);
        }

        #endregion

        #region data

        private readonly XmlPackageReferenceVersion _LocalReference;
        private readonly string _Source;
        private readonly IReadOnlyList<NuGet.Versioning.NuGetVersion> _AvailableVersions;

        #endregion

        #region Properties

        public ICommand ApplyVersionCmd { get; }

        public string Name => _LocalReference.PackageId;

        public string Prefix => _LocalReference.PackagePrefix;

        public IEnumerable<string> AvailableVersions => _AvailableVersions.Select(item => item.ToString()).Reverse().ToArray();

        public string NewestRelease => _AvailableVersions.Where(item => !item.IsPrerelease).OrderBy(item => item).LastOrDefault()?.ToString();

        public string NewestPrerelease => _AvailableVersions.Where(item => item.IsPrerelease).OrderBy(item => item).LastOrDefault()?.ToString();

        public string Version
        {
            get { return _LocalReference.Version; }
            set
            {
                _LocalReference.Version = value;
                RaisePropertyChanged(nameof(Version));
                RaisePropertyChanged(nameof(IsUpToDate));
                RaisePropertyChanged(nameof(NeedsUpdate));
            }
        }

        public bool IsUpToDate => Version == AvailableVersions.FirstOrDefault();

        public bool HasVersionRange => _LocalReference.HasVersionRange;

        public bool NeedsUpdate => !IsUpToDate && !HasVersionRange;

        public bool IsUser => !IsSystem && !IsTest;

        public bool IsSystem => !IsTest && (Constants.SystemPackages.Contains(Name) || Constants.SystemPrefixes.Any(p => Name.StartsWith(p + ".")));

        public bool IsTest => Constants.TestPackages.Contains(Name) || Constants.TestPrefixes.Any(p => Name.StartsWith(p + "."));

        #endregion
    }
}
