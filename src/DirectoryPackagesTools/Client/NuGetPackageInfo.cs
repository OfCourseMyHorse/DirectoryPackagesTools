using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Plugins;
using NuGet.Versioning;



namespace DirectoryPackagesTools.Client
{
    /// <summary>
    /// Metadata information associated to a package ID
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Id}")]
    public class NuGetPackageInfo
    {
        #region lifecycle

        internal static async Task<NuGetPackageVersionInfo[]> CreateAsync(IReadOnlyList<IPackageReferenceVersion> locals, NuGetClient client, IProgress<int> progress, CancellationToken? ctoken = null)
        {
            var tmp = locals
                .Select(kvp => new NuGetPackageInfo(kvp.PackageId))
                .ToArray();

            await UpdateAsync(tmp, client, progress, ctoken);

            return locals
                .Select(kvp => tmp.FirstOrDefault(item => item.Id == kvp.PackageId)[kvp.Version.MinVersion])
                .ToArray();
        }

        public static async Task UpdateAsync(IReadOnlyList<NuGetPackageInfo> packages, NuGetClient client, IProgress<int> progress, CancellationToken? ctoken = null)
        {
            await UpdateAsync(packages, client, progress);
        }

        /// <summary>
        /// Updates the versions of all the packages found in <paramref name="packages"/>
        /// </summary>
        /// <param name="packages">The package versions to be updated</param>
        /// <param name="progress">reports progress to the client</param>
        /// <param name="token"></param>        
        public static async Task UpdateAsync(IReadOnlyList<NuGetPackageInfo> packages, NuGetClient client, IProgress<int> progress)
        {
            var percent = new _ProgressCounter(progress, packages.Count);

            foreach (var package in packages)
            {
                await package.UpdateAsync(client);

                percent.Report(package.Id);
            }
        }        

        public static async Task<NuGetPackageInfo> CreateAsync(NuGetClient client, string id)
        {
            var instance = new NuGetPackageInfo(id);
            await instance.UpdateAsync(client);
            return instance;
        }

        public NuGetPackageInfo(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        

        public async Task UpdateAsync(NuGetClient client)
        {
            _Client = client;

            var repos = _CachedRepos.Count == 0

                // First call will scan all repos
                ? client.Repositories

                // subsequent calls will use cached repos
                : _CachedRepos
                    .Keys
                    .Select(repoName => client.Repositories.FirstOrDefault(repo => repo.Source.PackageSource.Name == repoName))
                    .ToArray();

            foreach (var repo in repos)
            {                
                if (!repo.IsNugetOrg && !repo.IsVisualStudio)
                {
                    // these packages are expected to be found only locally or in Nuget.Org,
                    // if found in other repos it's because they're betas (Microsoft.) or spoof attempts
                    if (Id.StartsWith("System.")) continue;
                    if (Id.StartsWith("Microsoft.")) continue;
                }

                // get package information from the current repo.
                // A package can have different dependencies, metadata and versions on different repos

                #if !DEBUG
                try {
                #endif

                await _UpdateAsync(repo);

                #if !DEBUG
                } catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"{repo.Source} + {Id} = \r\n{ex.Message}");
                }
                #endif
            }
        }

        private async Task _UpdateAsync(NuGetRepository repo)
        {
            // get all versions
            var vvv = await repo.GetVersionsAsync(this.Id);
            if (vvv == null || vvv.Length == 0) return;

            // cache repo
            _CachedRepos[repo.Source.PackageSource.Name] = true;

            bool newVersionsAdded = false;

            foreach (var v in vvv)
            {
                if (!_Versions.ContainsKey(v))
                {
                    _Versions[v] = new NuGetPackageVersionInfo(this, v);
                    newVersionsAdded = true;
                }
            }

            if (!newVersionsAdded) return; // nothing to do

            await NuGetPackageVersionInfo.UpdateMetadatas(_Versions, repo);            
        }

        #endregion

        #region data

        private NuGetClient _Client;

        public string Id { get; }

        /// <summary>
        /// List of Repo names where we've found info from this package
        /// </summary>
        internal readonly ConcurrentDictionary<string,bool> _CachedRepos = new ConcurrentDictionary<string,bool>();

        private readonly System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion, NuGetPackageVersionInfo> _Versions = new System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion, NuGetPackageVersionInfo>();

        #endregion

        #region properties

        public bool AllDeprecated => _Versions.Values.All(item => item.DeprecationInfo != null);

        public NuGetPackageVersionInfo this[NUGETVERSIONRANGE range]
        {
            get
            {
                var key = _Versions.Keys.OrderByDescending(item => item).FirstOrDefault(range.Satisfies);
                return this[key];
            }
        }

        public NuGetPackageVersionInfo this[NUGETVERSION version]
        {
            get
            {
                if (version == null) return null;
                return _Versions.TryGetValue(version, out var exact)
                    ? exact
                    : new NuGetPackageVersionInfo(this, version); // non existant version
            }
        }

        #endregion

        #region API

        public IReadOnlyList<NuGetVersion> GetVersions()
        {
            var allDeprecated = this.AllDeprecated;

            return _Versions
                .Where(item => allDeprecated || item.Value.DeprecationInfo == null)
                .Select(item => item.Key)
                .OrderBy(item => item)
                .ToList();
        }        

        /// <summary>
        /// Returns the first non null value found in the list of repos
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="callback"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        internal async Task<T> GetFirstAsync<T>(Func<NuGetRepository, Task<T>> callback, CancellationToken? token = null) where T:class
        {
            if (_Client == null) return null;

            foreach (var repo in _Client.FilterRepositories(_CachedRepos.Keys.ToImmutableHashSet()))
            {
                var result = await callback.Invoke(repo);

                if (result != null) return result;
            }

            return null;
        }

        internal async Task ForEachRepository(Func<NuGetRepository, Task<bool>> callback, CancellationToken? token = null)
        {
            if (_Client == null) return;            

            foreach (var repo in _Client.FilterRepositories(_CachedRepos.Keys.ToImmutableHashSet()))
            {
                var result = await callback.Invoke(repo);

                if (!result) break;
            }
        }

        #endregion
    }

    /// <summary>
    /// Metadata information of an exact version of a package
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Parent.Id} {Version}")]
    public class NuGetPackageVersionInfo
    {
        #region lifecycle

        internal static async Task UpdateMetadatas(IReadOnlyDictionary<NUGETVERSION, NuGetPackageVersionInfo> versions, NuGetRepository repo)
        {
            var ids = versions
                .Values
                .Select(item => item.Parent.Id)
                .Distinct();

            // we need the metadata straight away because we need to retrieve the tags
            // which are used for initial package classification.

            foreach (var id in ids)
            {
                var mmm = await repo.GetMetadataAsync(id);

                foreach (var metaData in mmm)
                {
                    if (!versions.TryGetValue(metaData.Identity.Version, out var extras)) continue;

                    await extras.UpdateAsync(metaData);
                }                
            }
        }

        public NuGetPackageVersionInfo(NuGetPackageInfo parent, NUGETVERSION version)
        {
            Parent = parent;
            Version = version;

            _Dependencies = new AsyncLazy<NUGETPACKDEPENDENCIES>(_GetDependenciesAsync);
        }

        #endregion

        #region data

        public NuGetPackageInfo Parent { get; }
        public NuGetVersion Version { get; }

        public NUGETPACKMETADATA Metadata { get; private set; }
        public NUGETPACKDEPRECATION DeprecationInfo { get; private set; }

        private readonly AsyncLazy<NUGETPACKDEPENDENCIES> _Dependencies;        

        #endregion

        #region API

        internal async Task UpdateAsync(NUGETPACKMETADATA metaData)
        {
            Metadata = metaData;
            DeprecationInfo ??= await metaData.GetDeprecationMetadataAsync().ConfigureAwait(false);
        }

        public async Task<NUGETPACKDEPENDENCIES> GetDependenciesAsync() => await _Dependencies;

        private async Task<NUGETPACKDEPENDENCIES> _GetDependenciesAsync()
        {
            await Task.Yield();

            var pid = new PackageIdentity(Parent.Id, Version);            

            return await Parent.GetFirstAsync( async repo => await repo.GetDependencyInfoAsync(pid));
        }        

        #endregion
    }
}
