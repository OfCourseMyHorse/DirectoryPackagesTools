using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DirectoryPackagesTools.Client
{
    using NUGETVERSIONSBAG = System.Collections.Concurrent.ConcurrentBag<NuGetVersion>;

    /// <summary>
    /// Entry point for all nuget APIs
    /// </summary>
    public class NuGetClient
    {
        // https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk
        // https://github.com/NuGet/Samples/blob/main/NuGetProtocolSamples/Program.cs
        // https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

        #region lifecycle

        public NuGetClient() : this(null) { }        

        public NuGetClient(System.IO.DirectoryInfo dinfo, ILogger logger = null)
        {
            dinfo ??= new System.IO.DirectoryInfo(Environment.CurrentDirectory);

            Logger = logger ?? ProgressLogger.Instance;            

            Settings = NuGet.Configuration.Settings.LoadDefaultSettings(dinfo.FullName);
            var provider = new PackageSourceProvider(Settings);
            _ReposProvider = new SourceRepositoryProvider(provider, Repository.Provider.GetCoreV3());

            _Repos = new Lazy<SourceRepository[]>(() => _ReposProvider.GetRepositories().ToArray());
            _RepoAPIs = new Lazy<NuGetRepository[]>(() => _Repos.Value.Select(item => new NuGetRepository(item, null, Logger)).ToArray());
        }

        #endregion

        #region data

        public ILogger Logger { get; }

        private readonly SourceRepositoryProvider _ReposProvider;

        private Lazy<SourceRepository[]> _Repos;

        private Lazy<NuGetRepository[]> _RepoAPIs;        

        #endregion

        #region properties

        public ISettings Settings { get; }

        public IReadOnlyList<NuGetRepository> Repositories => _RepoAPIs.Value;        

        #endregion

        #region API

        internal async Task ForEachRepository(Func<NuGetRepository, Task<bool>> callback, IReadOnlyCollection<string> cachedRepos = null, CancellationToken? token = null)
        {
            await Task.Yield(); // ensure async

            foreach (var repo in FilterRepositories(cachedRepos ?? Array.Empty<string>()))
            {
                var result = await callback.Invoke(repo);

                if (!result) break;
            }
        }

        /// <summary>
        /// Gets all versions found in all registered repositories
        /// </summary>
        /// <param name="packageId">the package it for which we're querying the versions</param>
        /// <returns></returns>
        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId)
        {
            await Task.Yield(); // ensure async

            var bag = new NUGETVERSIONSBAG();

            foreach (var r in Repositories)
            {
                var vvv = await r.GetVersionsAsync(packageId);

                foreach (var v in vvv) bag.Add(v);
            }

            return bag.Distinct().ToArray();
        }

        public IEnumerable<NuGetRepository> FilterRepositories(IReadOnlyCollection<string> repoNames)
        {
            if (repoNames.Count == 0) return Repositories;

            return repoNames
                .Select(n => Repositories.FirstOrDefault(item => item.Source.PackageSource.Name == n))
                .Where(item => item != null);
        }

        public async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(PackageIdentity package)
        {
            await Task.Yield(); // ensure async

            foreach (var api in Repositories)
            {
                var dinfo = await api.GetDependencyInfoAsync(package);
                if (dinfo != null) return dinfo;
            }

            return null;
        }

        public async Task<PackageArchiveReader> DownloadPackageArchiveReaderAsync(PackageIdentity package)
        {
            await Task.Yield(); // ensure async

            foreach (var api in Repositories)
            {
                var pkg = await api.DownloadPackageToPackageArchiveReaderAsync(package);
                if (pkg != null) return pkg;
            }

            return null;
        }

        #endregion
    }    
}
