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
        public NuGetPackageInfo(string id, VersionRange version)
        {
            Id = id;
            _CurrVersion = version;
        }

        public string Id { get; }

        private VersionRange _CurrVersion;

        private readonly NUGETVERSIONSBAG _Versions = new NUGETVERSIONSBAG();

        public NuGet.Protocol.Core.Types.IPackageSearchMetadata Metadata { get; private set; }

        public NuGet.Protocol.Core.Types.FindPackageByIdDependencyInfo Dependencies { get; private set; }

        public IReadOnlyList<NuGetVersion> GetVersions() => _Versions.OrderBy(item => item).ToList();        

        public async Task UpdateAsync(NuGetClientContext client)
        {
            var pid = new PackageIdentity(Id, _CurrVersion.MinVersion);            

            var mmm = await client.GetMetadataAsync(pid).ConfigureAwait(false);
            Metadata = mmm.FirstOrDefault();

            Dependencies = await client.GetDependencyInfoAsync(pid).ConfigureAwait(false);

            foreach (var api in client.Repositories)
            {
                var vvv = await api.GetVersionsAsync(this.Id).ConfigureAwait(false);

                foreach (var v in vvv)
                {
                    if (!_Versions.Contains(v)) _Versions.Add(v);
                }                
            }
        }
    }
}
