﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

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

            var dict = locals.ToDictionary(kvp => kvp.PackageId, kvp => new System.Collections.Concurrent.ConcurrentBag<NuGetVersion>());

            await client.GetVersions(dict, progress);            

            foreach (var local in dict)
            {
                var package = locals.FirstOrDefault(item => item.PackageId == local.Key);
                var versions = local.Value.Distinct().OrderBy(item =>item).ToList();

                var mvvm = new PackageMVVM(package, null, versions);

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

        public string DocumentPath => _Path.FullName;

        public IEnumerable<SourceRepository> Repositories => _Client.Repositories;

        public IReadOnlyList<PackageMVVM> AllPackages => _Packages;

        public IEnumerable<KeyValuePair<string, PackageMVVM[]>> GroupedPackages
        {
            get
            {
                // find package prefixes shared between at least 3 packages
                var commonPrefixes = AllPackages
                    .GroupBy(item => item.Prefix).Where(item => item.Count() >= 3)
                    .Select(item => item.Key)
                    .ToArray();

                // group key evaluator
                string getGroupKey(PackageMVVM mvvm)
                {
                    if (mvvm.IsSystem) return "System";
                    if (mvvm.IsTest) return "Test";

                    if (commonPrefixes.Contains(mvvm.Prefix)) return mvvm.Prefix;

                    return "User";
                }

                return AllPackages
                    .GroupBy(getGroupKey)
                    .ToDictionary(item => item.Key, item => item.ToArray());
            }
        }

        #endregion
    }

    [System.Diagnostics.DebuggerDisplay("{Name} {Version}")]
    public class PackageMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle
        internal PackageMVVM(PackageReferenceVersion local, string source, IReadOnlyList<NuGet.Versioning.NuGetVersion> versions)
        {
            _LocalReference = local;
            _AvailableVersions = versions;
            _Source = source;

            ApplyVersionCmd = new Prism.Commands.DelegateCommand<string>( ver => this.Version = ver );
        }

        #endregion

        #region data

        private readonly PackageReferenceVersion _LocalReference;
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

        public bool IsSystem => !IsTest && (Constants.SystemPackages.Contains(Name) || Constants.SystemPrefixes.Any(p => Name.StartsWith(p+".")));

        public bool IsTest => Constants.TestPackages.Contains(Name) || Constants.TestPrefixes.Any(p => Name.StartsWith(p + "."));

        #endregion
    }
}
