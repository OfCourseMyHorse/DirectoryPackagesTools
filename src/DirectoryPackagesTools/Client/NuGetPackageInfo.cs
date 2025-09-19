using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Packaging.Core;
using NuGet.Protocol.Plugins;
using NuGet.Versioning;



namespace DirectoryPackagesTools.Client
{
    /// <summary>
    /// Metadata information associated to a package
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
            ctoken ??= CancellationToken.None;

            using (var ctx = client.CreateContext(ctoken))
            {
                await UpdateAsync(packages, ctx, progress);
            }
        }

        /// <summary>
        /// Updates the versions of all the packages found in <paramref name="packages"/>
        /// </summary>
        /// <param name="packages">The package versions to be updated</param>
        /// <param name="progress">reports progress to the client</param>
        /// <param name="token"></param>        
        public static async Task UpdateAsync(IReadOnlyList<NuGetPackageInfo> packages, NuGetClientContext context, IProgress<int> progress)
        {
            var percent = new _ProgressCounter(progress, packages.Count);

            foreach (var package in packages)
            {
                await package.UpdateAsync(context);

                percent.Report(package.Id);
            }
        }

        public static async Task<NuGetPackageInfo> CreateAsync(NuGetClient client, string id, CancellationToken? ctoken = null)
        {
            ctoken ??= CancellationToken.None;

            using (var ctx = client.CreateContext(ctoken))
            {
                return await CreateAsync(ctx, id);
            }
        }

        public static async Task<NuGetPackageInfo> CreateAsync(NuGetClientContext context, string id)
        {
            var instance = new NuGetPackageInfo(id);
            await instance.UpdateAsync(context);
            return instance;
        }

        public NuGetPackageInfo(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public async Task UpdateAsync(NuGetClient client, CancellationToken? ctoken = null)
        {
            ctoken ??= CancellationToken.None;

            using var ctx = client.CreateContext(ctoken);
            await UpdateAsync(ctx);
        }

        public async Task UpdateAsync(NuGetClientContext client)
        {
            foreach (var repo in client.Repositories)
            {
                if (!repo.IsNugetOrg && !repo.IsVisualStudio)
                {
                    if (Id.StartsWith("System.")) continue;
                    if (Id.StartsWith("Microsoft.")) continue;
                }

                // get package information from the current repo.
                // A package can have different dependencies, metadata and versions on different repos

                #if !DEBUG
                try
                {
                #endif
                    // get all versions
                    var vvv = await repo.GetVersionsAsync(this.Id).ConfigureAwait(false);
                    if (vvv == null || vvv.Length == 0) continue;

                    bool newVersionsAdded = false;

                    foreach (var v in vvv)
                    {
                        if (!_Versions.ContainsKey(v))
                        {
                            _Versions[v] = new NuGetPackageVersionInfo(this, v);
                            newVersionsAdded = true;
                        }
                    }

                    if (!newVersionsAdded) continue; // nothing to do

                    await NuGetPackageVersionInfo.UpdateMetadatas(_Versions, repo);

            #if !DEBUG
            } catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"{repo.Source} + {Id} = \r\n{ex.Message}");
                }
            #endif
            }
        }

        #endregion

        #region data

        public string Id { get; }


        private readonly System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion, NuGetPackageVersionInfo> _Versions = new System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion, NuGetPackageVersionInfo>();

        // public NUGETPACKMETADATA Metadata => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.Metadata : null;

        // public NUGETPACKDEPRECATION DeprecationInfo => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.DeprecationInfo : null;        

        public bool AllDeprecated => _Versions.Values.All(item => item.DeprecationInfo != null);

        // public NUGETPACKDEPENDENCIES Dependencies => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.Dependencies : null;

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

        public NuGetPackageVersionInfo this[NUGETVERSION version]
        {
            get
            {
                return _Versions.TryGetValue(version, out var exact)
                    ? exact
                    : new NuGetPackageVersionInfo(this, version); // non existant version
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents the exact version of a package
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Parent.Id} {Version}")]
    public class NuGetPackageVersionInfo
    {
        #region lifecycle

        internal static async Task UpdateMetadatas(IReadOnlyDictionary<NUGETVERSION, NuGetPackageVersionInfo> versions, SourceRepositoryAPI repo)
        {
            var ids = versions.Values.Select(item => item.Parent.Id).Distinct();

            foreach (var id in ids)
            {
                var mmm = await repo.GetMetadataAsync(id);

                foreach (var metaData in mmm)
                {
                    if (!versions.TryGetValue(metaData.Identity.Version, out var extras)) continue;

                    await extras.UpdateAsync(metaData);
                }

                // get dependencies ONLY for the current version                    

                /*
                if (Dependencies == null)
                {
                    if (versions.TryGetValue(_PackageId.Version, out var extras))
                    {
                        await extras.UpdateDependenciesAsync(repo);
                    }
                }*/
            }
        }

        public NuGetPackageVersionInfo(NuGetPackageInfo parent, NUGETVERSION version)
        {
            Parent = parent;
            Version = version;
        }

        #endregion

        #region data

        public NuGetPackageInfo Parent { get; }
        public NuGetVersion Version { get; }

        public NUGETPACKMETADATA Metadata { get; private set; }

        public NUGETPACKDEPRECATION DeprecationInfo { get; private set; }

        public NUGETPACKDEPENDENCIES Dependencies { get; private set; }

        #endregion

        #region API

        internal async Task UpdateAsync(NUGETPACKMETADATA metaData)
        {
            Metadata = metaData;
            DeprecationInfo ??= await metaData.GetDeprecationMetadataAsync().ConfigureAwait(false);
        }

        internal async Task UpdateDependenciesAsync(SourceRepositoryAPI repo)
        {
            var pid = new PackageIdentity(Parent.Id, Version);

            this.Dependencies = await repo.GetDependencyInfoAsync(pid).ConfigureAwait(false);
        }

        #endregion
    }
}
