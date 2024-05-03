using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace DirectoryPackagesTools.Client
{
    using NUGETVERSIONSBAG = System.Collections.Concurrent.ConcurrentBag<NuGetVersion>;

    [System.Diagnostics.DebuggerDisplay("{Id}")]
    public class NuGetPackageInfo
    {
        #region lifecycle
        public NuGetPackageInfo(string id, VersionRange version)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            _CurrVersion = version ?? throw new ArgumentNullException(nameof(version));
        }

        #endregion

        #region data

        public string Id { get; }

        private VersionRange _CurrVersion;

        private readonly NUGETVERSIONSBAG _Versions = new NUGETVERSIONSBAG();

        public NuGet.Protocol.Core.Types.IPackageSearchMetadata Metadata { get; private set; }

        public NuGet.Protocol.Core.Types.FindPackageByIdDependencyInfo Dependencies { get; private set; }

        #endregion

        #region API

        public IReadOnlyList<NuGetVersion> GetVersions() => _Versions.OrderBy(item => item).ToList();        

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
                    if (Dependencies == null)
                    {
                        var deps = await repo.GetDependencyInfoAsync(pid).ConfigureAwait(false);
                        // if (deps == null) continue;
                        Dependencies ??= deps;
                    }
                    
                    if (Metadata == null)
                    {
                        var mmm = await repo.GetMetadataAsync(pid).ConfigureAwait(false);
                        Metadata ??= mmm;
                    }                    

                    var vvv = await repo.GetVersionsAsync(this.Id).ConfigureAwait(false);                    

                    foreach (var v in vvv)
                    {
                        if (!_Versions.Contains(v)) _Versions.Add(v);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"{repo.Source} + {pid.Id} = \r\n{ex.Message}");
                }
            }
        }

        #endregion
    }
}
