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
        public NuGetPackageInfo(string id, string version)
        {
            Id = id;
            CurrentVersion = new NuGetVersion(version);
        }

        public string Id { get; }

        public NuGetVersion CurrentVersion { get; }

        private readonly NUGETVERSIONSBAG _Versions = new NUGETVERSIONSBAG();

        public IReadOnlyList<NuGetVersion> GetVersions() => _Versions.OrderBy(item => item).ToList();

        public NuGet.Protocol.Core.Types.FindPackageByIdDependencyInfo CurrentDependencies { get; private set; }

        public async Task UpdateAsync(NuGetClientContext client)
        {
            foreach (var api in client.Repositories)
            {
                var vvv = await api.GetVersionsAsync(this.Id);

                foreach (var v in vvv)
                {
                    if (_Versions.Contains(v)) return;
                    _Versions.Add(v);
                }

                if (CurrentVersion != null)
                {
                    var pid = new NuGet.Packaging.Core.PackageIdentity(Id, CurrentVersion);

                    CurrentDependencies = await api.GetDependencyInfoAsync(pid);
                }
            }
        }
    }
}
