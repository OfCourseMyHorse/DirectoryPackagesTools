using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DirectoryPackagesTools.Client
{
    using NUGETVERSIONSBAG = System.Collections.Concurrent.ConcurrentBag<NuGetVersion>;

    public class NuGetClient : BaseMVVM
    {
        // https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk
        // https://github.com/NuGet/Samples/blob/main/NuGetProtocolSamples/Program.cs
        // https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

        #region lifecycle

        public NuGetClient() : this((string)null) { }

        public NuGetClient(System.IO.DirectoryInfo dinfo) : this(dinfo.FullName) { }

        public NuGetClient(string root)
        {
            Settings = NuGet.Configuration.Settings.LoadDefaultSettings(root);
            var provider = new PackageSourceProvider(Settings);

            _Repos = new SourceRepositoryProvider(provider, Repository.Provider.GetCoreV3());
            Logger = NullLogger.Instance;            
        }

        #endregion

        #region data

        private SourceRepositoryProvider _Repos;        
        public ILogger Logger { get; }

        #endregion

        #region properties

        public ISettings Settings { get; }

        public IEnumerable<SourceRepository> Repositories => _Repos.GetRepositories();

        public TimeSpan LastOperationTime { get; private set; }

        #endregion

        #region API

        internal async Task ForEachRepository(Func<SourceRepositoryAPI, Task<bool>> callback, IReadOnlyCollection<string> cachedRepos = null, CancellationToken? token = null)
        {
            using var ctx = CreateContext(token ?? CancellationToken.None);

            foreach (var repo in ctx.FilterRepositories(cachedRepos ?? Array.Empty<string>()))
            {
                var result = await callback.Invoke(repo);

                if (!result) break;
            }
        }

        public NuGetClientContext CreateContext(CancellationToken? token)
        {
            token ??= CancellationToken.None;
            return new NuGetClientContext(this._Repos, this.Logger, token.Value);            
        }        

        #endregion
    }

    /// <summary>
    /// Exposes the API of the nuget client
    /// </summary>
    /// <remarks>
    /// Created by <see cref="NuGetClient.CreateContext(CancellationToken?)"/>
    /// </remarks>
    public class NuGetClientContext : IDisposable
    {
        #region lifecycle

        internal NuGetClientContext(SourceRepositoryProvider repos, ILogger logger, CancellationToken token)
        {
            

            _Repos = repos;
            Logger = logger;
            _Cache = NullSourceCacheContext.Instance;
            _Token = token;

            _RepoAPIs = _Repos
                .GetRepositories()
                .Select(item => new SourceRepositoryAPI(this, item))
                .OrderByDescending(item => item.PriorityScore)
                .ToArray();            
        }

        public void Dispose()
        {
            _OnDispose?.Invoke();
            _OnDispose = null;

            _Cache?.Dispose();
            _Cache = null;

            _Repos = null;            
        }

        #endregion

        #region data

        private Action _OnDispose;

        internal CancellationToken _Token;
        internal SourceCacheContext _Cache;

        private SourceRepositoryProvider _Repos;

        private readonly SourceRepositoryAPI[] _RepoAPIs;

        #endregion

        #region properties        

        public ILogger Logger { get; }
        
        public IReadOnlyList<SourceRepositoryAPI> Repositories => _RepoAPIs;

        #endregion

        #region API

        /// <summary>
        /// Gets all versions found in all registered repositories
        /// </summary>
        /// <param name="packageId">the package it for which we're querying the versions</param>
        /// <returns></returns>
        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId)
        {
            var bag = new NUGETVERSIONSBAG();

            foreach (var r in Repositories)
            {
                var vvv = await r.GetVersionsAsync(packageId);

                foreach (var v in vvv) bag.Add(v);
            }

            return bag.Distinct().ToArray();
        }

        public IEnumerable<SourceRepositoryAPI> FilterRepositories(IReadOnlyCollection<string> repoNames)
        {
            if (repoNames.Count == 0) return Repositories;

            return repoNames
                .Select(n => Repositories.FirstOrDefault(item => item.Source.PackageSource.Name == n))
                .Where(item => item != null);
        }        

        

        public async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(PackageIdentity package)
        {
            foreach (var api in Repositories)
            {
                var dinfo = await api.GetDependencyInfoAsync(package);
                if (dinfo != null) return dinfo;
            }

            return null;
        }

        #endregion
    }

    [System.Diagnostics.DebuggerDisplay("{_Repo.PackageSource}")]
    public class SourceRepositoryAPI
    {
        #region lifecycle

        internal SourceRepositoryAPI(NuGetClientContext context, SourceRepository repo)
        {
            _Context = context;
            _Repo = repo;
        }

        #endregion

        #region data

        private readonly NuGetClientContext _Context;
        private readonly SourceRepository _Repo;
        private readonly Dictionary<Type, INuGetResource> _APIsCache = new Dictionary<Type, INuGetResource>();
        public SourceRepository Source => _Repo;

        #endregion

        #region properties

        public bool IsOfficial => _Repo.PackageSource.IsOfficial;

        public bool IsLocal => _Repo.PackageSource.IsLocal;

        public bool IsNugetOrg => !_Repo.PackageSource.IsLocal && _Repo.PackageSource.SourceUri.DnsSafeHost.EndsWith("nuget.org");

        public bool IsVisualStudio => _Repo.PackageSource.IsLocal && _Repo.PackageSource.Name == "Microsoft Visual Studio Offline Packages";

        public float PriorityScore
        {
            get
            {
                if (IsLocal) return 10000;
                if (IsVisualStudio) return 1000;
                if (IsNugetOrg) return 100;
                if (IsOfficial) return 10;
                return 0;
            }
        }

        #endregion

        #region core

        private async Task<T> _GetAPIAsync<T>()
            where T : class, INuGetResource
        {
            if (_Context._Cache == null) throw new ObjectDisposedException("Context");

            if (_APIsCache.TryGetValue(typeof(T), out var api)) return await Task.FromResult(api as T);

            api = await _Repo.GetResourceAsync<T>(_Context._Token).ConfigureAwait(false);
            if (api == null) return null;

            _APIsCache[typeof(T)] = api;

            return api as T;
        }

        #endregion

        #region API

        public async Task<IReadOnlyList<IPackageSearchMetadata>> SearchAsync(SearchFilter filter, string searchTerm = null)
        {
            var api = await _GetAPIAsync<PackageSearchResource>();

            int index = 0;
            int step = 20;

            var collection = new List<IPackageSearchMetadata>();

            while(true)
            {
                var result = await api.SearchAsync(searchTerm, filter, index, step, _Context.Logger, _Context._Token);

                var count = collection.Count;
                collection.AddRange(result);
                if (collection.Count == count) break;
                
                index += step;
            }

            collection.Sort(PackageSearchMetadataComparer.Default);

            return collection;
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(SearchFilter filter, string searchTerm, int skip, int take)
        {
            searchTerm ??= string.Empty;
            if (searchTerm == "*") searchTerm = string.Empty;

            var api = await _GetAPIAsync<PackageSearchResource>();

            return await api.SearchAsync(searchTerm, filter, skip, take, _Context.Logger, _Context._Token);
        }

        #endregion

        #region all versions package API

        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId, bool includePrerelease, bool includeUnlisted)
        {
            var resM = await _GetAPIAsync<MetadataResource>();

            var vvv = await resM.GetVersions(packageId, includePrerelease, includeUnlisted, _Context._Cache, _Context.Logger, _Context._Token);

            return vvv.ToArray();
        }


        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId)
        {
            var resPID = await _GetAPIAsync<FindPackageByIdResource>();

            var vvv = await resPID.GetAllVersionsAsync(packageId, _Context._Cache, _Context.Logger, _Context._Token);

            return vvv == null
                ? Array.Empty<NuGetVersion>()
                : vvv.ToArray();
        }

        public async Task<IPackageSearchMetadata[]> GetMetadataAsync(string packageId, bool includePrerelease = true, bool includeUnlisted = true)
        {
            var resPM = await _GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>();

            var psm = await resPM.GetMetadataAsync(packageId, includePrerelease, includeUnlisted, _Context._Cache, _Context.Logger, _Context._Token);

            return psm.ToArray();
        }

        #endregion

        #region exact package API

        public async Task<bool?> ExistLocally(PackageIdentity package)
        {
            var resFLP = await _GetAPIAsync<FindLocalPackagesResource>(); // only local repos implement this API
            if (resFLP != null) return resFLP.Exists(package, _Context.Logger, _Context._Token);            

            return null;
        }

        public async Task<IPackageSearchMetadata> GetMetadataAsync(PackageIdentity package)
        {
            var resPM = await _GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>();

            return await resPM.GetMetadataAsync(package, _Context._Cache, _Context.Logger, _Context._Token);
        }

        public async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(PackageIdentity package)
        {
            var resPID = await _GetAPIAsync<FindPackageByIdResource>();

            return await resPID.GetDependencyInfoAsync(package.Id, package.Version, _Context._Cache, _Context.Logger, _Context._Token);
        }

        public async Task<SourcePackageDependencyInfo> GetPackageDependenciesAsync(PackageIdentity package, NuGetFramework framework)
        {
            var dependencyInfoResource = await _GetAPIAsync<DependencyInfoResource>();

            return await dependencyInfoResource.ResolvePackage(package, framework, _Context._Cache, _Context.Logger, _Context._Token);
        }

        #endregion                    
    }
}
