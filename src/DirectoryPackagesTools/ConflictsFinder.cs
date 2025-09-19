using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using NuGet.Protocol.Core.Types;
using NuGet.Packaging.Core;
using NuGet.Frameworks;
using NuGet.Packaging;

using DirectoryPackagesTools.Client;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Used to identify dependency conflicts in a package dependency tree
    /// </summary>
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
            foreach (var pkg in _Groups.Keys.OrderBy(item => item))
            {
                var dict = new Dictionary<PackageIdentity, (PackageDependencyGroup, PackageDependency)>();
                ContainsDependency(pkg, "System.Memory", dict);

                if (dict.Count == 0) continue;

                System.Diagnostics.Trace.WriteLine("");
                System.Diagnostics.Trace.WriteLine("");
                System.Diagnostics.Trace.WriteLine(pkg.ToString());
                foreach (var kvp in dict)
                {
                    System.Diagnostics.Trace.WriteLine($"    {kvp.Value.Item1.TargetFramework}: {kvp.Key}   {kvp.Value.Item2}");
                }
            }
        }

        public void ContainsDependency(PackageIdentity packageIdentity, string dependency, Dictionary<PackageIdentity, (PackageDependencyGroup, PackageDependency)> result)
        {
            if (!_Groups.TryGetValue(packageIdentity, out var deps)) return;

            foreach (var pkg in deps.Packages)
            {
                if (pkg.Id == dependency)
                {
                    result[packageIdentity] = (deps, pkg);
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
