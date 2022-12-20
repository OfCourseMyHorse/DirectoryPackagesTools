using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

using NuGet.Versioning;

namespace DirectoryPackagesTools
{
    public class NuGetClient
    {
        // https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk
        // https://github.com/NuGet/Samples/blob/main/NuGetProtocolSamples/Program.cs
        // https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

        public NuGetClient() : this(null) { }

        public NuGetClient(string root)
        {
            var settings = Settings.LoadDefaultSettings(root);
            _Repos = new SourceRepositoryProvider(settings, Repository.Provider.GetCoreV3());
            _Logger = NullLogger.Instance;
        }


        private SourceRepositoryProvider _Repos;
        private ILogger _Logger;

        public IEnumerable<SourceRepository> Repositories => _Repos.GetRepositories();

        public async Task ResolvePackage(PackageIdentity package, NuGetFramework framework)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                foreach (var sourceRepository in _Repos.GetRepositories())
                {
                    var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                    var dependencyInfo = await dependencyInfoResource.ResolvePackage(
                        package, framework, cacheContext, _Logger, CancellationToken.None);

                    if (dependencyInfo != null)
                    {
                        Console.WriteLine(dependencyInfo);
                        return;
                    }
                }
            }
        }

        public async Task<NuGetVersion[]> GetVersions(string packageName, CancellationToken? token = null)
        {
            token ??= CancellationToken.None;

            var versions = new HashSet<NuGetVersion>();

            using (var cacheContext = new SourceCacheContext())
            {
                foreach (var sourceRepository in _Repos.GetRepositories())
                {
                    var resource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>();

                    var vvv = await resource.GetAllVersionsAsync(packageName, cacheContext, _Logger, token.Value);

                    versions.UnionWith(vvv);
                }
            }

            return versions.ToArray();
        }
    }
}
