using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using NuGet.Protocol.Core.Types;


using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.DOM;
using NuGet.Packaging.Core;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;
using System.Threading.Tasks.Sources;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// MVVM view over a <see cref="XmlPackagesVersionsProjectDOM"/>
    /// </summary>
    /// <remarks>
    /// This is the MVVM of a Directory.Packages.props project
    /// </remarks>
    public class PackagesVersionsProjectMVVM : Prism.Mvvm.BindableBase
    {
        #region lifecycle

        public static void WriteNewVersionsProject(System.IO.FileInfo finfo, bool updateProjects)
        {
            XmlPackagesVersionsProjectDOM.CreateVersionFileFromExistingProjects(finfo);

            if (updateProjects)
            {
                XmlProjectDOM.RemoveVersionsFromProjectsFiles(finfo.Directory);
            }
        }

        public static async Task<PackagesVersionsProjectMVVM> LoadAsync(string filePath, IProgress<int> progress, CancellationToken ctoken)
        {
            // Load Directory.Packages.Props
            var dom = XmlPackagesVersionsProjectDOM.Load(filePath);

            var client = new NuGetClient(dom.File.Directory.FullName);

            // Load all *.csproj within the directory
            var csprojs = await XmlProjectDOM
                .EnumerateProjects(dom.File.Directory, true)
                .Where(item => item.ManagePackageVersionsCentrally)
                .ToListAsync(progress)
                .ConfigureAwait(true);

            // verify

            var err = dom.VerifyDocument(csprojs);

            if (err != null)
            {
                if (progress is IProgress<Exception> exrep)
                {
                    exrep.Report(new InvalidOperationException(err));
                }

                return null;
            }

            // retrieve versions from nuget repositories.

            
            var packages = await _GetPackagesAsync(dom, client, progress, ctoken);
            
            // add dependencies

            foreach(var csproj in csprojs)
            {
                foreach(var pkg in csproj.GetPackageReferences())
                {
                    var dst = packages.FirstOrDefault(item => item.Name == pkg.PackageId);
                    dst._AddDependent(csproj);
                }
            }

            return new PackagesVersionsProjectMVVM(dom, client, packages);
        }

        private static async Task<PackageMVVM[]> _GetPackagesAsync(XmlPackagesVersionsProjectDOM dom, NuGetClient client, IProgress<int> progress, CancellationToken ctoken)
        {
            var locals = dom
                .GetPackageReferences()
                .ToList();            

            var tmp = locals
                .Select(kvp => new NuGetPackageInfo(kvp.PackageId, kvp.Version))
                .ToArray();

            using(var ctx = client.CreateContext(ctoken))
            {
                await ctx.FillVersionsAsync(tmp, progress);
            }

            var mvvms = new List<PackageMVVM>();

            foreach (var pinfo in tmp)
            {
                var package = locals.First(item => item.PackageId == pinfo.Id);                

                var mvvm = new PackageMVVM(package, pinfo);

                mvvms.Add(mvvm);
            }

            return mvvms.OrderBy(item => item.Name).ToArray();
        }

        public void Save()
        {
            _Dom.Save(null);
        }

        private PackagesVersionsProjectMVVM(XmlPackagesVersionsProjectDOM dom, NuGetClient client, PackageMVVM[] packages)
        {            
            _Dom = dom;
            _Client = client;
            _Packages = packages;
        }

        #endregion

        #region data
        
        private readonly XmlPackagesVersionsProjectDOM _Dom;
        private readonly NuGetClient _Client;

        private readonly PackageMVVM[] _Packages;

        #endregion

        #region API

        public System.IO.FileInfo File => _Dom.File;

        public string DocumentPath => _Dom.File.FullName;

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

        #region extras

        public void RestoreVersionsToProjects()
        {
            var packages = AllPackages
                .ToDictionary(item => item.Name, item => item.Version.ToString());

            XmlProjectDOM.RestoreVersionsToProjectsFiles(File.Directory, packages);
        }

        public async Task RefreshPackageDependenciesAsync(IProgress<int> progress, CancellationToken ctoken)
        {
            var fff = new[]
            {
                FrameworkConstants.CommonFrameworks.NetStandard10,
                FrameworkConstants.CommonFrameworks.NetStandard11,
                FrameworkConstants.CommonFrameworks.NetStandard12,
                FrameworkConstants.CommonFrameworks.NetStandard13,
                FrameworkConstants.CommonFrameworks.NetStandard14,
                FrameworkConstants.CommonFrameworks.NetStandard15,
                FrameworkConstants.CommonFrameworks.NetStandard16,
                FrameworkConstants.CommonFrameworks.NetStandard17,                
                FrameworkConstants.CommonFrameworks.NetStandard20,
                FrameworkConstants.CommonFrameworks.Net45,
                FrameworkConstants.CommonFrameworks.Net451,
                FrameworkConstants.CommonFrameworks.Net452,
                FrameworkConstants.CommonFrameworks.Net46,
                FrameworkConstants.CommonFrameworks.Net461,
                FrameworkConstants.CommonFrameworks.Net462,
                FrameworkConstants.CommonFrameworks.Net463,
                FrameworkConstants.CommonFrameworks.Net47,
                FrameworkConstants.CommonFrameworks.Net471,
                FrameworkConstants.CommonFrameworks.Net472,
            };


            using (var ctx = _Client.CreateContext(ctoken))
            {
                var conflictsFinder = new ConflictsFinder(ctx, fff);

                // populate

                for (int i=0; i < _Packages.Length; i++)
                {
                    progress?.Report(i*100 / _Packages.Length);

                    await conflictsFinder.AddPackageAsync(_Packages[i].GetCurrentIdentity());                    
                }

                // run

                conflictsFinder.FindConflicts();
            }
        }

        #endregion
    }

    class ConflictsFinder
    {
        public ConflictsFinder(NuGetClientContext context, params NuGetFramework[] frameworks)
        {
            _Context = context;
            _Frameworks = frameworks;
        }

        private readonly NuGetClientContext _Context;

        private readonly NuGetFramework[] _Frameworks;

        private readonly Dictionary<PackageIdentity, FindPackageByIdDependencyInfo> _Packages = new Dictionary<PackageIdentity, FindPackageByIdDependencyInfo>();

        private readonly Dictionary<PackageIdentity, PackageDependencyGroup> _Groups = new Dictionary<PackageIdentity, PackageDependencyGroup>();

        public async Task AddPackageAsync(PackageIdentity packageIdentity)
        {
            if (_Packages.ContainsKey(packageIdentity)) return;

            var deps = await _Context.GetDependencyInfoAsync(packageIdentity);
            if (deps == null) return;

            _Packages[packageIdentity] = deps;

            var group = deps.DependencyGroups
                .Select(item => (item, Array.IndexOf(_Frameworks, item.TargetFramework)))
                .OrderBy(item => item.Item2)
                .Select(item => item.item)
                .LastOrDefault();

            if (group == null) return; // not compatible

            _Groups[packageIdentity] = group;

            foreach (var dep in group.Packages)
            {
                var pid = new PackageIdentity(dep.Id, dep.VersionRange.MinVersion);

                await AddPackageAsync(pid);
            }
        }

        public void FindConflicts()
        {
            foreach(var pkg in _Groups.Keys.OrderBy(item => item))
            {
                var dict = new Dictionary<PackageIdentity, (PackageDependencyGroup, PackageDependency)>();
                ContainsDependency(pkg, "System.Memory", dict);                

                if (dict.Count == 0) continue;

                System.Diagnostics.Trace.WriteLine("");
                System.Diagnostics.Trace.WriteLine("");
                System.Diagnostics.Trace.WriteLine(pkg.ToString());
                foreach(var kvp in dict)
                {
                    System.Diagnostics.Trace.WriteLine($"    {kvp.Value.Item1.TargetFramework}: {kvp.Key}   {kvp.Value.Item2}");
                }
            }
        }

        public void ContainsDependency(PackageIdentity packageIdentity, string dependency, Dictionary<PackageIdentity, (PackageDependencyGroup, PackageDependency)> result)
        {
            if (!_Groups.TryGetValue(packageIdentity, out var deps)) return;

            foreach(var pkg in deps.Packages)
            {
                if (pkg.Id == dependency)
                {
                    result[packageIdentity] = (deps,pkg);
                }
                else
                {
                    var pid = new PackageIdentity(pkg.Id, pkg.VersionRange.MinVersion);

                    ContainsDependency(pid, dependency, result);
                }
            }            
        }

        
    }
}

