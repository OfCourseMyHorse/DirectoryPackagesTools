using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Protocol.Core.Types;
using NuGet.Frameworks;

using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.DOM;

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
                .EnumerateProjects(dom.File.Directory)
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
                var classifier = new PackageClassifier(AllPackages.Select(item => item.Prefix));

                return AllPackages
                    .GroupBy(p => p.GetPackageCategory(classifier))
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

    
}

