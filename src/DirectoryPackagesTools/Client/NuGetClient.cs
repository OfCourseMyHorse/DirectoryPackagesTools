using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

    public class NuGetClient : Prism.Mvvm.BindableBase
    {
        // https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk
        // https://github.com/NuGet/Samples/blob/main/NuGetProtocolSamples/Program.cs
        // https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

        #region lifecycle

        public NuGetClient() : this(null) { }

        public NuGetClient(string root)
        {
            var settings = Settings.LoadDefaultSettings(root);
            var provider = new PackageSourceProvider(settings);

            _Repos = new SourceRepositoryProvider(provider, Repository.Provider.GetCoreV3());
            Logger = NullLogger.Instance;
        }

        #endregion

        #region data

        private SourceRepositoryProvider _Repos;

        public ILogger Logger { get; }

        #endregion

        #region properties

        public IEnumerable<SourceRepository> Repositories => _Repos.GetRepositories();

        public TimeSpan LastOperationTime { get; private set; }

        #endregion

        #region API        

        public NuGetClientContext CreateContext(CancellationToken? token)
        {
            token ??= CancellationToken.None;

            return new NuGetClientContext(this._Repos, this.Logger, token.Value);
        }        

        public async Task<SourcePackageDependencyInfo> ResolvePackage(PackageIdentity package, NuGetFramework framework)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                foreach (var sourceRepository in _Repos.GetRepositories())
                {
                    var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                    var dependencyInfo = await dependencyInfoResource.ResolvePackage(package, framework, cacheContext, Logger, CancellationToken.None);

                    if (dependencyInfo != null) return dependencyInfo;
                }

                return null;
            }
        }


        public async Task<Dictionary<NuGetFramework,SourcePackageDependencyInfo>> ResolvePackage(PackageIdentity package)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                foreach (var sourceRepository in _Repos.GetRepositories())
                {
                    var resDIR = await sourceRepository.GetResourceAsync<DependencyInfoResource>();                    
                }

                return null;
            }
        }

        #endregion
    }

    public class NuGetClientContext : IDisposable
    {
        #region lifecycle

        internal NuGetClientContext(SourceRepositoryProvider repos, ILogger logger, CancellationToken token)
        {
            _Repos = repos;
            Logger = logger;
            _Cache = new SourceCacheContext();
            _Token = token;

            _RepoAPIs = _Repos
                .GetRepositories()
                .Select(item => new SourceRepositoryAPI(this, item))
                .ToArray();            
        }

        public void Dispose()
        {
            _Cache?.Dispose();
            _Cache = null;

            _Repos = null;
        }

        #endregion

        #region data

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

        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId)
        {
            var bag = new NUGETVERSIONSBAG();

            foreach(var r in Repositories)
            {
                var vvv = await r.GetVersionsAsync(packageId);
                
                foreach(var v in vvv) bag.Add(v);
            }

            return bag.Distinct().ToArray();
        }

        /// <summary>
        /// Updates the versions of all the packages found in <paramref name="packages"/>
        /// </summary>
        /// <param name="packages">The package versions to be updated</param>
        /// <param name="progress">reports progress to the client</param>
        /// <param name="token"></param>        
        public async Task FillVersionsAsync(IReadOnlyList<NuGetPackageInfo> packages, IProgress<int> progress)
        {
            var percent = new _ProgressCounter(progress, packages.Count);

            foreach (var package in packages)
            {
                await package.UpdateVersionsAsync(this);

                percent.Report(package.Id);
            }
        }

        #endregion
    }

    [System.Diagnostics.DebuggerDisplay("{_Repo.PackageSource}")]
    public class SourceRepositoryAPI
    {
        internal SourceRepositoryAPI(NuGetClientContext context, SourceRepository repo)
        {
            _Context = context;
            _Repo = repo;
        }

        private readonly NuGetClientContext _Context;
        private readonly SourceRepository _Repo;
        private readonly Dictionary<Type, INuGetResource> _APIs = new Dictionary<Type, INuGetResource>();

        public SourceRepository Source => _Repo;

        private async Task<T> GetAPIAsync<T>()
            where T : class, INuGetResource
        {
            if (_APIs.TryGetValue(typeof(T), out var api)) return await Task.FromResult(api as T);

            api = await _Repo.GetResourceAsync<T>(_Context._Token).ConfigureAwait(false);
            if (api == null) return null;

            _APIs[typeof(T)] = api;

            return api as T;
        }

        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId)
        {
            var resPID = await GetAPIAsync<FindPackageByIdResource>().ConfigureAwait(false);

            var vvv = await resPID.GetAllVersionsAsync(packageId, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);

            return vvv.ToArray();
        }

        public async Task<bool?> ExistLocally(PackageIdentity package)
        {
            var resFLP = await GetAPIAsync<FindLocalPackagesResource>().ConfigureAwait(false);
            if (resFLP != null) return resFLP.Exists(package, _Context.Logger, _Context._Token);

            var resM = await GetAPIAsync<MetadataResource>().ConfigureAwait(false);
            if (resM != null) return await resM.Exists(package, _Context._Cache, _Context.Logger, _Context._Token);

            return null;
        }       

        public async Task<IPackageSearchMetadata[]> GetMetadataAsync(string packageId, bool includePrerelease = true, bool includeUnlisted = true)
        {
            var resPM = await GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>().ConfigureAwait(false);

            var psm = await resPM.GetMetadataAsync(packageId, includePrerelease, includeUnlisted, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);

            return psm.ToArray();
        }

        public async Task<IPackageSearchMetadata> GetMetadataAsync(PackageIdentity package)
        {
            var resPM = await GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>().ConfigureAwait(false);

            return await resPM.GetMetadataAsync(package, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);            
        }        
    }


    [System.Diagnostics.DebuggerDisplay("{Id}")]
    public class NuGetPackageInfo
    {
        public NuGetPackageInfo(string id) { Id = id; }

        public string Id { get; }

        private readonly NUGETVERSIONSBAG _Versions = new NUGETVERSIONSBAG();

        public IReadOnlyList<NuGetVersion> GetVersions() => _Versions.OrderBy(item => item).ToList();        
        
        internal void AddVersions(IEnumerable<NuGetVersion> versions)
        {
            foreach(var v in versions)
            {
                if (_Versions.Contains(v)) return;
                _Versions.Add(v);
            }
        }

        /// <summary>
        /// Gets all the versions available for a given package.
        /// </summary>
        /// <param name="packageName">The package name; 'System.Numerics.Vectors'</param>
        /// <param name="token"></param>
        /// <returns>A list of available versions</returns>
        public async Task UpdateVersionsAsync(NuGetClientContext client)
        {
            foreach (var api in client.Repositories)
            {
                var vvv = await api.GetVersionsAsync(this.Id);

                AddVersions(vvv);
            }
        }
    }
}
