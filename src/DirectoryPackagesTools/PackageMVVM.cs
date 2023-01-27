using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using DirectoryPackagesTools.DOM;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// MVVM view over <see cref="XmlPackageReferenceVersion"/>
    /// </summary>
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

        private readonly HashSet<XmlProjectDOM> _ProjectsUsingThis = new HashSet<XmlProjectDOM>();

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

        public int NumProjectsInUse => _ProjectsUsingThis.Count;

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
            }
        }

        public IEnumerable<string> AvailableVersions => _AvailableVersions.Select(item => item.ToString()).Reverse().ToList();

        public string NewestRelease => _AvailableVersions.Where(item => !item.IsPrerelease).OrderBy(item => item).LastOrDefault()?.ToString();

        public string NewestPrerelease => _AvailableVersions.Where(item => item.IsPrerelease).OrderBy(item => item).LastOrDefault()?.ToString();        

        public bool VersionIsUpToDate => Version == AvailableVersions.FirstOrDefault();

        public bool HasVersionRange => _LocalReference.HasVersionRange;

        public bool NeedsUpdate => !VersionIsUpToDate && !HasVersionRange;

        #endregion

        #region API

        internal void _AddDependent(XmlProjectDOM prj)
        {
            _ProjectsUsingThis.Add(prj);
        }

        #endregion
    }
}
