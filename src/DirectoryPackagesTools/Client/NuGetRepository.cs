using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DirectoryPackagesTools.Client
{
    /// <summary>
    /// Wrapper over <see cref="SourceRepository"/>
    /// </summary>
    /// <remarks>
    /// Retrieved from <see cref="NuGetClient.Repositories"/>
    /// </remarks>
    [System.Diagnostics.DebuggerDisplay("{_Repo.PackageSource}")]
    public class NuGetRepository
    {
        #region lifecycle

        internal NuGetRepository(SourceRepository repo, SourceCacheContext cache = null, ILogger logger = null)
        {
            ArgumentNullException.ThrowIfNull(repo);

            _Cache = cache ?? NullSourceCacheContext.Instance;
            _Repo = repo;
            _Logger = logger ?? ProgressLogger.Instance;

            _Semaphore = new SemaphoreSlim(1);
        }

        #endregion

        #region data

        private readonly SemaphoreSlim _Semaphore;

        private readonly SourceRepository _Repo;
        private readonly SourceCacheContext _Cache;
        private readonly ILogger _Logger;

        private readonly Dictionary<Type, INuGetResource> _APIsCache = new Dictionary<Type, INuGetResource>();

        #endregion

        #region properties

        public SourceRepository Source => _Repo;

        /// <summary>
        /// This is an official repository (Local VisualStudio, NuGet)
        /// </summary>
        public bool IsOfficial => _Repo.PackageSource.IsOfficial;

        /// <summary>
        /// Its a locally cached repository
        /// </summary>
        public bool IsLocal => _Repo.PackageSource.IsLocal;

        public bool IsNugetOrg => !_Repo.PackageSource.IsLocal && _Repo.PackageSource.SourceUri.DnsSafeHost.EndsWith("nuget.org");

        public bool IsVisualStudio => _Repo.PackageSource.IsLocal && _Repo.PackageSource.Name == "Microsoft Visual Studio Offline Packages";

        /// <summary>
        /// When looking for a package, it determines the priority score.
        /// </summary>
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

        private async Task<T> _GetAPIAsync<T>(CancellationToken? token = null)
            where T : class, INuGetResource
        {
            if (_APIsCache.TryGetValue(typeof(T), out var api)) return await Task.FromResult(api as T);

            api = await _Repo.GetResourceAsync<T>(token ?? CancellationToken.None).ConfigureAwait(false);
            if (api == null) return null;

            _APIsCache[typeof(T)] = api;

            return api as T;
        }

        #endregion

        #region API

        private async Task<T> _ThrottleAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken? token = null)
        {
            token ??= CancellationToken.None;

            if (!await _Semaphore.WaitAsync(20000, token.Value)) return default;

            try
            {
                // we do need to run in a thread to prevent UI freezing
                return await Task.Run(async ()=> await action.Invoke(token.Value));
            }
            finally
            {
                _Semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<IPackageSearchMetadata>> SearchAsync(SearchFilter filter, string searchTerm = null, CancellationToken? token = null)
        {
            var api = await _GetAPIAsync<PackageSearchResource>();

            int index = 0;
            int step = 20;

            var collection = new List<IPackageSearchMetadata>();

            while (true)
            {
                var result = await api.SearchAsync(searchTerm, filter, index, step, _Logger, token ?? CancellationToken.None);

                var count = collection.Count;
                collection.AddRange(result);
                if (collection.Count == count) break;

                index += step;
            }

            collection.Sort(PackageSearchMetadataComparer.Default);

            return collection;
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(SearchFilter filter, string searchTerm, int skip, int take, CancellationToken? token = null)
        {
            searchTerm ??= string.Empty;
            if (searchTerm == "*") searchTerm = string.Empty;

            var api = await _GetAPIAsync<PackageSearchResource>();

            return await api.SearchAsync(searchTerm, filter, skip, take, _Logger, token ?? CancellationToken.None);
        }        

        #endregion

        #region packageId API

        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId, bool includePrerelease, bool includeUnlisted, CancellationToken? token = null)
        {
            await Task.Yield(); // ensure async

            var resM = await _GetAPIAsync<MetadataResource>();

            var vvv = await resM.GetVersions(packageId, includePrerelease, includeUnlisted, _Cache, _Logger, token ?? CancellationToken.None);

            return vvv.ToArray();
        }


        public async Task<NuGetVersion[]> GetVersionsAsync(string packageId, CancellationToken? token = null)
        {
            await Task.Yield(); // ensure async

            var resPID = await _GetAPIAsync<FindPackageByIdResource>();

            var vvv = await resPID.GetAllVersionsAsync(packageId, _Cache, _Logger, token ?? CancellationToken.None);

            return vvv == null
                ? Array.Empty<NuGetVersion>()
                : vvv.ToArray();
        }

        public async Task<IPackageSearchMetadata[]> GetMetadataAsync(string packageId, bool includePrerelease = true, bool includeUnlisted = true, CancellationToken? token = null)
        {
            await Task.Yield(); // ensure async

            var resPM = await _GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>();

            var psm = await resPM.GetMetadataAsync(packageId, includePrerelease, includeUnlisted, _Cache, _Logger, token ?? CancellationToken.None);

            return psm.ToArray();
        }

        #endregion

        #region PackageIdentity API

        public async Task<bool?> ExistLocally(PackageIdentity package, CancellationToken? token = null)
        {
            await Task.Yield(); // ensure async

            var resFLP = await _GetAPIAsync<FindLocalPackagesResource>(); // only local repos implement this API
            if (resFLP != null) return resFLP.Exists(package, _Logger, token ?? CancellationToken.None);

            return null;
        }

        public async Task<IPackageSearchMetadata> GetMetadataAsync(PackageIdentity package, CancellationToken? token = null)
        {
            return await _ThrottleAsync( async t =>
            {
                var resPM = await _GetAPIAsync<NuGet.Protocol.Core.Types.PackageMetadataResource>();
                return await resPM.GetMetadataAsync(package, _Cache, _Logger, t);
            }, token);
        }

        public async Task<FindPackageByIdDependencyInfo> GetDependencyInfoAsync(PackageIdentity package, CancellationToken? token = null)
        {
            return await _ThrottleAsync(async t =>
            {
                // on average, this takes 200ms , but with the ONNXRuntime packages it can take up to full 4 seconds.

                var resPID = await _GetAPIAsync<FindPackageByIdResource>();
                return await resPID.GetDependencyInfoAsync(package.Id, package.Version, _Cache, _Logger, t);

            }, token);
        }

        public async Task<SourcePackageDependencyInfo> GetPackageDependenciesAsync(PackageIdentity package, NuGetFramework framework, CancellationToken? token = null)
        {
            return await _ThrottleAsync(async t =>
            {
                var dependencyInfoResource = await _GetAPIAsync<DependencyInfoResource>();
                return await dependencyInfoResource.ResolvePackage(package, framework, _Cache, _Logger, t);
            }, token);
        }

        public async Task<IPackageDownloader> GetPackageDownloaderAsync(PackageIdentity package, CancellationToken? token = null)
        {
            return await _ThrottleAsync(async t =>
            {
                var api = await _GetAPIAsync<FindPackageByIdResource>();
                if (api == null) return null;

                return await api.GetPackageDownloaderAsync(package, _Cache, _Logger, t);
            }, token);
        }

        public async Task<System.IO.Compression.ZipArchive> DownloadPackageToZipAsync(PackageIdentity package, CancellationToken? token = null)
        {
            var m = await DownloadPackageToStreamAsync(package, token);
            if (m == null) return null;

            return new System.IO.Compression.ZipArchive(m, System.IO.Compression.ZipArchiveMode.Read);
        }

        public async Task<PackageArchiveReader> DownloadPackageToPackageArchiveReaderAsync(PackageIdentity package, CancellationToken? token = null)
        {
            var m = await DownloadPackageToStreamAsync(package, token);
            if (m == null) return null;

            return new PackageArchiveReader(m);
        }

        public async Task<System.IO.MemoryStream> DownloadPackageToStreamAsync(PackageIdentity package, CancellationToken? token = null)
        {
            return await _ThrottleAsync(async t =>
            {
                var api = await _GetAPIAsync<FindPackageByIdResource>();
                if (api == null) return null;

                var m = new System.IO.MemoryStream();

                if (await api.CopyNupkgToStreamAsync(package.Id, package.Version, m, _Cache, _Logger, t)) return m;

                m.Dispose();
                return null;

            }, token);
        }

        #endregion                    
    }
}
