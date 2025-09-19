using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.DOM;

using NuGet.Commands;
using NuGet.Frameworks;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Plugins;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// MVVM view over a <see cref="XmlPackagesVersionsProjectDOM"/>
    /// </summary>
    /// <remarks>
    /// This is the MVVM of a Directory.Packages.props project
    /// </remarks>
    [System.Diagnostics.DebuggerDisplay("{DocumentPath}")]
    public class PackagesVersionsProjectMVVM : BaseMVVM
    {
        #region lifecycle

        public static void WriteNewVersionsProject(System.IO.FileInfo finfo, bool updateProjects)
        {
            XmlPackagesVersionsProjectDOM.CreateVersionFileFromExistingProjects(finfo);

            if (updateProjects)
            {
                XmlMSBuildProjectDOM.RemoveVersionsFromProjectsFiles(finfo.Directory);
            }
        }

        public static PackagesVersionsProjectMVVM FromDirectory(string directoryPath)
        {
            var client = new NuGetClient(directoryPath);

            return new PackagesVersionsProjectMVVM(null, client, null);
        }

        public static async Task<PackagesVersionsProjectMVVM> LoadAsync(string filePath, IProgress<int> progress, CancellationToken ctoken)
        {
            // read document

            var isJson = filePath.ToLower().EndsWith(".json");

            IPackageVersionsProject xdom = null;
            List<XmlMSBuildProjectDOM> csprojs = null;

            if (isJson)
            {
                xdom = JsonToolsVersionsProjectDOM.Load(filePath);
            }            
            else
            {
                // Load Directory.Packages.Props
                var dom = XmlPackagesVersionsProjectDOM.Load(filePath);
                xdom = dom;

                // Load all *.csproj within the directory
                csprojs = await XmlMSBuildProjectDOM
                    .EnumerateProjects(xdom.File.Directory)
                    .Where(item => !item.IsLegacyProject && item.ManagePackageVersionsCentrally)
                    .ToListAsync(progress)
                    .ConfigureAwait(false);

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

                // also load the json

                var dnt = MergedPackagesVersions.GetDotNetToolsFileFor(filePath);

                if (dnt != null)
                {
                    var jdom = JsonToolsVersionsProjectDOM.Load(dnt.FullName);
                    if (jdom != null) xdom = new MergedPackagesVersions(dom, jdom);
                }
            }            

            // retrieve versions from nuget repositories.

            var client = new NuGetClient(xdom.File.Directory.FullName);

            var packages = await _GetPackagesAsync(xdom, client, progress, ctoken).ConfigureAwait(false);

            // add dependencies

            if (csprojs != null)
            {
                foreach (var csproj in csprojs)
                {
                    foreach (var pkg in csproj.GetPackageReferences())
                    {
                        var dst = packages.FirstOrDefault(item => item.Name == pkg.PackageId);
                        dst._AddDependent(csproj);
                    }
                }
            }

            return new PackagesVersionsProjectMVVM(xdom, client, packages);
        }

        private static async Task<PackageMVVM[]> _GetPackagesAsync(IPackageVersionsProject dom, NuGetClient client, IProgress<int> progress, CancellationToken ctoken)
        {
            var locals = dom
                .GetPackageReferences()
                .ToList();

            var tmp = await NuGetPackageInfo.CreateAsync(locals, client, progress, ctoken);

            var mvvms = new List<PackageMVVM>();

            foreach (var pinfo in tmp)
            {
                var package = locals.First(item => item.PackageId == pinfo.Id);                

                var mvvm = new PackageMVVM(package, pinfo, client);

                mvvms.Add(mvvm);
            }

            return mvvms.OrderBy(item => item.Name).ToArray();
        }

        public void Save()
        {
            _Dom.Save(null);
        }

        private PackagesVersionsProjectMVVM(IPackageVersionsProject dom, NuGetClient client, PackageMVVM[] packages)
        {            
            _Dom = dom ?? IPackageVersionsProject.Empty;
            _Client = client;
            _Packages = packages ?? Array.Empty<PackageMVVM>();
        }

        #endregion

        #region data

        private readonly NuGetClient _Client;

        private readonly IPackageVersionsProject _Dom;
        private readonly PackageMVVM[] _Packages;

        #endregion

        #region Properties

        public System.IO.FileInfo File => _Dom.File;
        public string DocumentPath => _Dom.File.FullName;

        public RepositoriesCollectionMVVM Repositories => new RepositoriesCollectionMVVM(_Client);

        /// <summary>
        /// Gets all the packages.
        /// </summary>
        public IReadOnlyList<PackageMVVM> AllPackages => _Packages;

        /// <summary>
        /// Gets all the packages grouped by category.
        /// </summary>
        public IEnumerable<PackagesGroupMVVM> GroupedPackages => PackagesGroupMVVM.CreateGroups(AllPackages);       

        public IEnumerable<KeyedViewMVVM> Views => GroupedPackages.Cast<KeyedViewMVVM>().Append(Repositories);

        #endregion

        #region extras

        public void RestoreVersionsToProjects()
        {
            var packages = AllPackages
                .ToDictionary(item => item.Name, item => item.Version.ToString());

            XmlMSBuildProjectDOM.RestoreVersionsToProjectsFiles(File.Directory, packages);
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


    public abstract class KeyedViewMVVM : BaseMVVM
    {
        protected KeyedViewMVVM(string key) { Key = key; }
        public string Key { get; }
    }

    
    /// <summary>
    /// Represents a group of packages that share a given category (testing, logging, etc)
    /// </summary>
    public class PackagesGroupMVVM : KeyedViewMVVM
    {
        #region lifecycle
        public static IEnumerable<PackagesGroupMVVM> CreateGroups(IReadOnlyCollection<PackageMVVM> allPackages)
        {
            var classifier = new Utils.PackageClassifier(allPackages.Select(item => item.Prefix));

            return allPackages
                .GroupBy(p => p.GetPackageCategory(classifier))
                .Select(item => new PackagesGroupMVVM(item.Key, item))
                .ToList();
        }

        private PackagesGroupMVVM(string key, IEnumerable<PackageMVVM> values) : base(key)
        {            
            Packages = values.ToList();
        }
        #endregion

        #region data
        public IReadOnlyList<PackageMVVM> Packages { get; }

        #endregion

    }
}

