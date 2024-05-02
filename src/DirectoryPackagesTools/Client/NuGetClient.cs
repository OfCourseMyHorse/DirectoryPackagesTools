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

        #endregion
    }

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
                await package.UpdateAsync(this).ConfigureAwait(true);

                percent.Report(package.Id);                                
            }
        }

        public async Task<IReadOnlyList<IPackageSearchMetadata>> GetMetadataAsync(params PackageIdentity[] packages)
        {
            var result = new List<IPackageSearchMetadata>();

            foreach(var package in packages)
            {
                foreach (var api in Repositories)
                {
                    var data = await api.GetMetadataAsync(package);
                    if (data != null)
                    {
                        result.Add(data);
                        break;
                    }                        
                }
            }

            return result;
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

        #region API

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

        public async Task<bool?> ExistLocally(PackageIdentity package)
        {
            var resFLP = await _GetAPIAsync<FindLocalPackagesResource>().ConfigureAwait(false); // only local repos implement this API
            if (resFLP != null) return resFLP.Exists(package, _Context.Logger, _Context._Token);            

            return null;
        }


        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId, bool includePrerelease, bool includeUnlisted)
        {
            var resM = await _GetAPIAsync<MetadataResource>().ConfigureAwait(false);            

            var vvv = await resM.GetVersions(packageId, includePrerelease, includeUnlisted, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);

            return vvv.ToArray();
        }


        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId)
        {
            var resPID = await _GetAPIAsync<FindPackageByIdResource>().ConfigureAwait(false);

            var vvv = await resPID.GetAllVersionsAsync(packageId, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);

            return vvv.ToArray();
        }

        public async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(PackageIdentity package)
        {
            var resPID = await _GetAPIAsync<FindPackageByIdResource>().ConfigureAwait(false);

            return await resPID.GetDependencyInfoAsync(package.Id, package.Version, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);
        }

        public async Task<IPackageSearchMetadata[]> GetMetadataAsync(string packageId, bool includePrerelease = true, bool includeUnlisted = true)
        {
            var resPM = await _GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>().ConfigureAwait(false);

            var psm = await resPM.GetMetadataAsync(packageId, includePrerelease, includeUnlisted, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);

            return psm.ToArray();
        }

        public async Task<IPackageSearchMetadata> GetMetadataAsync(PackageIdentity package)
        {
            var resPM = await _GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>().ConfigureAwait(false);

            return await resPM.GetMetadataAsync(package, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);            
        }

        public async Task<SourcePackageDependencyInfo> GetPackageDependenciesAsync(PackageIdentity package, NuGetFramework framework)
        {
            var dependencyInfoResource = await _GetAPIAsync<DependencyInfoResource>().ConfigureAwait(false);

            return await dependencyInfoResource.ResolvePackage(package, framework, _Context._Cache, _Context.Logger, _Context._Token).ConfigureAwait(false);
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filter, int skip, int take)
        {
            var api = await _GetAPIAsync<PackageSearchResource>().ConfigureAwait(false);

            return await api.SearchAsync(searchTerm, filter, skip, take, _Context.Logger, _Context._Token);
        }

        #endregion             
    }
}
