using System;
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
    /// <summary>
    /// MVVM view over a <see cref="XmlPackagesVersionsProjectDOM"/>
    /// </summary>
    public class PackagesVersionsProjectMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle

        public static async Task<PackagesVersionsProjectMVVM> Load(string filePath, IProgress<int> progress)
        {
            var path = new FileInfo(filePath);
            var dom = XmlPackagesVersionsProjectDOM.Load(path.FullName);

            var err = dom.VerifyDocument();
            if (err != null)
            {
                if (progress is IProgress<Exception> exrep)
                {
                    exrep.Report(new InvalidOperationException(err));
                }

                return null;
            }

            var client = new NuGetClient(path.Directory.FullName);
            var packages = await _GetPackagesAsync(dom, client, progress);

            return new PackagesVersionsProjectMVVM(path, dom, client, packages);
        }

        private static async Task<PackageMVVM[]> _GetPackagesAsync(XmlPackagesVersionsProjectDOM dom, NuGetClient client, IProgress<int> progress)
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

        private PackagesVersionsProjectMVVM(System.IO.FileInfo finfo, XmlPackagesVersionsProjectDOM dom, NuGetClient client, PackageMVVM[] packages)
        {
            _Path =finfo;
            _Dom = dom;
            _Client = client;
            _Packages = packages;
        }

        #endregion

        #region data

        private readonly System.IO.FileInfo _Path;
        private readonly XmlPackagesVersionsProjectDOM _Dom;
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
}
