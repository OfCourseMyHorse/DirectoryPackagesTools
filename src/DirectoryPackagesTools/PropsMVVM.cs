using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using NuGet.Protocol.Core.Types;

namespace DirectoryPackagesTools
{
    public class PropsMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle

        public static async Task<PropsMVVM> Load(string filePath, IProgress<int> progress)
        {
            var path = new FileInfo(filePath);
            var dom = PropsDOM.Load(path.FullName);

            var client = new NuGetClient(path.Directory.FullName);
            var packages = await _GetPackagesAsync(dom, client, progress);

            return new PropsMVVM(path, dom, client, packages);
        }

        private static async Task<PackageMVVM[]> _GetPackagesAsync(PropsDOM dom, NuGetClient client, IProgress<int> progress)
        {
            var locals = dom.GetPackageReferences().ToList();

            var mvvms = new List<PackageMVVM>();

            int count = 0;

            foreach (var local in locals)
            {
                progress?.Report((count++ * 100) / locals.Count);

                var versions = await client.GetVersions(local.PackageId);

                var mvvm = new PackageMVVM(local, versions);

                mvvms.Add(mvvm);
            }

            return mvvms.OrderBy(item => item.Name).ToArray();
        }

        public void Save()
        {
            _Dom.Save(_Path.FullName);
        }

        private PropsMVVM(System.IO.FileInfo finfo, PropsDOM dom, NuGetClient client, PackageMVVM[] packages)
        {
            _Path =finfo;
            _Dom = dom;
            _Client = client;
            _Packages = packages;
        }

        #endregion

        #region data

        private readonly System.IO.FileInfo _Path;
        private readonly PropsDOM _Dom;
        private readonly NuGetClient _Client;

        private readonly PackageMVVM[] _Packages;

        #endregion

        #region API

        public IEnumerable<SourceRepository> Repositories => _Client.Repositories;

        public IReadOnlyList<PackageMVVM> AllPackages => _Packages;

        public IEnumerable<PackageMVVM> SystemPackages => AllPackages.Where(item => item.IsSystem);

        public IEnumerable<PackageMVVM> TestPackages => AllPackages.Where(item => item.IsTest);

        public IEnumerable<PackageMVVM> UserPackages => AllPackages.Where(item => item.IsUser);

        #endregion
    }

    [System.Diagnostics.DebuggerDisplay("{Name} {Version}")]
    public class PackageMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle
        internal PackageMVVM(PackageReferenceVersion local, IReadOnlyList<NuGet.Versioning.NuGetVersion> versions)
        {
            _LocalReference = local;
            _AvailableVersions = versions;

            ApplyVersionCmd = new Prism.Commands.DelegateCommand<string>( ver => this.Version = ver );
        }

        #endregion

        #region data

        private readonly PackageReferenceVersion _LocalReference;
        private readonly IReadOnlyList<NuGet.Versioning.NuGetVersion> _AvailableVersions;

        #endregion

        #region Properties

        public ICommand ApplyVersionCmd { get; }

        public string Name => _LocalReference.PackageId;

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
        public bool NeedsUpdate => !IsUpToDate;

        public bool IsUser => !IsSystem && !IsTest;

        public bool IsSystem
            => Name.StartsWith("System.")
            || Name.StartsWith("Microsoft.")
            || Name.StartsWith("Azure.")
            || Name.StartsWith("Google.")
            || Name.StartsWith("Prism.");

        public bool IsTest
            => Name.StartsWith("coverlet.")
            || Name.StartsWith("NUnit")
            || Name.StartsWith("Microsoft.Net.Test.SDK");

        #endregion
    }
}
