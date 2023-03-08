using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        }

        public string Id { get; }        

        private readonly NUGETVERSIONSBAG _Versions = new NUGETVERSIONSBAG();

        public IReadOnlyList<NuGetVersion> GetVersions() => _Versions.OrderBy(item => item).ToList();

        

        public async Task UpdateAsync(NuGetClientContext client)
        {
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
