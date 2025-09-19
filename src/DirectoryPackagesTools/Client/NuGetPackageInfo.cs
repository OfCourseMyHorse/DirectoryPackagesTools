using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        internal static async Task<NuGetPackageInfo[]> CreateAsync(IReadOnlyList<IPackageReferenceVersion> locals, NuGetClient client, IProgress<int> progress, CancellationToken? ctoken = null)
        {
            var tmp = locals
                .Select(kvp => new NuGetPackageInfo(kvp.PackageId, kvp.Version))
                .ToArray();

            await UpdateAsync(tmp, client, progress, ctoken);

            return tmp;
        }

        public static async Task UpdateAsync(IReadOnlyList<NuGetPackageInfo> packages, NuGetClient client, IProgress<int> progress, CancellationToken? ctoken = null)
        {
            ctoken ??= CancellationToken.None;

            using(var cyx = client.CreateContext(ctoken))
            {
                await UpdateAsync(packages, cyx, progress);
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

        public static async Task<NuGetPackageInfo> CreateAsync(NuGetClient client, string id, VersionRange currentVersion, CancellationToken? ctoken = null)
        {
            ctoken ??= CancellationToken.None;

            using (var ctx = client.CreateContext(ctoken))
            {
                return await CreateAsync(ctx, id, currentVersion);
            }
        }

        public static async Task<NuGetPackageInfo> CreateAsync(NuGetClientContext context, string id, VersionRange currentVersion)
        {
            var instance = new NuGetPackageInfo(id,currentVersion);
            await instance.UpdateAsync(context);
            return instance;
        }        

        public NuGetPackageInfo(string id, VersionRange currentVersion)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _CurrVersion = currentVersion ?? throw new ArgumentException($"Invalid version for package {id}", nameof(currentVersion));

            _PackageId = new PackageIdentity(Id, _CurrVersion.MinVersion);
        }

        public async Task UpdateAsync(NuGetClient client, CancellationToken? ctoken = null)
        {
            ctoken ??= CancellationToken.None;

            using var ctx = client.CreateContext(ctoken);
            await UpdateAsync(ctx);
        }

        public async Task UpdateAsync(NuGetClientContext client)
        {
            var pid = new PackageIdentity(Id, _CurrVersion.MinVersion);

            foreach (var repo in client.Repositories)
            {
                if (!repo.IsNugetOrg && !repo.IsVisualStudio)
                {
                    if (pid.Id.StartsWith("System.")) continue;
                    if (pid.Id.StartsWith("Microsoft.")) continue;
                }

                try // get package information from the current repo. A package can have different dependencies, metadata and versions on different repos
                {
                    // get all versions
                    var vvv = await repo.GetVersionsAsync(this.Id).ConfigureAwait(false);
                    if (vvv == null || vvv.Length == 0) continue;

                    bool newVersionsAdded = false;

                    foreach (var v in vvv)
                    {
                        if (!_Versions.ContainsKey(v))
                        {
                            _Versions[v] = new _Extras();
                            newVersionsAdded = true;
                        }
                    }

                    if (!newVersionsAdded) continue; // nothing to do

                    // get all metadatas
                    var mmm = await repo.GetMetadataAsync(this.Id).ConfigureAwait(false);

                    foreach (var metaData in mmm)
                    {
                        if (!_Versions.TryGetValue(metaData.Identity.Version, out var extras)) continue;

                        extras.Metadata = metaData;
                        extras.DeprecationInfo ??= await metaData.GetDeprecationMetadataAsync().ConfigureAwait(false);
                    }

                    // get dependencies ONLY for the current version                    

                    if (Dependencies == null)
                    {
                        var deps = await repo.GetDependencyInfoAsync(pid).ConfigureAwait(false);
                        if (deps != null)
                        {
                            if (_Versions.TryGetValue(_PackageId.Version, out var extras))
                            {
                                extras.Dependencies = deps;
                            }
                        }

                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"{repo.Source} + {pid.Id} = \r\n{ex.Message}");
                }
            }
        }

        #endregion

        #region data

        public string Id { get; }

        private VersionRange _CurrVersion;
        private PackageIdentity _PackageId;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion,_Extras> _Versions = new System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion, _Extras>();

        public NUGETPACKMETADATA Metadata => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.Metadata : null;

        public NUGETPACKDEPRECATION DeprecationInfo => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.DeprecationInfo : null;        

        public bool AllDeprecated => _Versions.Values.All(item => item.DeprecationInfo != null);

        public NUGETPACKDEPENDENCIES Dependencies => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.Dependencies : null;

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

        

        #endregion

        #region nested type

        class _Extras
        {
            public NUGETPACKMETADATA Metadata { get; set; }

            public NUGETPACKDEPRECATION DeprecationInfo { get; set; }

            public NUGETPACKDEPENDENCIES Dependencies { get; set; }
        }

        #endregion
    }


}
