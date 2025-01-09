using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Packaging.Core;
using NuGet.Versioning;

using NPMETADATA = NuGet.Protocol.Core.Types.IPackageSearchMetadata;
using NPDEPRECATION = NuGet.Protocol.PackageDeprecationMetadata;
using NPDEPENDENCIES = NuGet.Protocol.Core.Types.FindPackageByIdDependencyInfo;

namespace DirectoryPackagesTools.Client
{
    /// <summary>
    /// Metadata information associated to a package
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Id}")]
    public class NuGetPackageInfo
    {
        #region lifecycle
        public NuGetPackageInfo(string id, VersionRange version)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _CurrVersion = version ?? throw new ArgumentException($"Invalid version for package {id}", nameof(version));

            _PackageId = new PackageIdentity(Id, _CurrVersion.MinVersion);
        }

        #endregion

        #region data

        public string Id { get; }

        private VersionRange _CurrVersion;
        private PackageIdentity _PackageId;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion,_Extras> _Versions = new System.Collections.Concurrent.ConcurrentDictionary<NuGetVersion, _Extras>();

        public NPMETADATA Metadata => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.Metadata : null;

        public NPDEPRECATION DeprecationInfo => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.DeprecationInfo : null;        

        public bool AllDeprecated => _Versions.Values.All(item => item.DeprecationInfo != null);

        public NPDEPENDENCIES Dependencies => _Versions.TryGetValue(_PackageId.Version, out var extras) ? extras.Dependencies : null;

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

                    foreach(var metaData in mmm)
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

        #region nested type

        class _Extras
        {
            public NPMETADATA Metadata { get; set; }

            public NPDEPRECATION DeprecationInfo { get; set; }

            public NPDEPENDENCIES Dependencies { get; set; }
        }

        #endregion
    }


}
