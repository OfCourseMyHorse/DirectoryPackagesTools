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
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DirectoryPackagesTools.Client
{
    public class NuGetClient : Prism.Mvvm.BindableBase
    {
        // https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk
        // https://github.com/NuGet/Samples/blob/main/NuGetProtocolSamples/Program.cs
        // https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

        #region lifecycle

        public NuGetClient() : this(null) { }

        public NuGetClient(string root)
        {
            var settings = Settings.LoadDefaultSettings(root);
            var provider = new PackageSourceProvider(settings);

            _Repos = new SourceRepositoryProvider(provider, Repository.Provider.GetCoreV3());
            _Logger = NullLogger.Instance;
        }

        #endregion

        #region data

        private SourceRepositoryProvider _Repos;
        private ILogger _Logger;

        #endregion

        #region properties

        public IEnumerable<SourceRepository> Repositories => _Repos.GetRepositories();

        public TimeSpan LastOperationTime { get; private set; }

        #endregion

        #region API        

        public async Task<SourcePackageDependencyInfo> ResolvePackage(PackageIdentity package, NuGetFramework framework)
        {
            using (var cacheContext = new SourceCacheContext())
            {
                foreach (var sourceRepository in _Repos.GetRepositories())
                {
                    var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                    var dependencyInfo = await dependencyInfoResource.ResolvePackage(package, framework, cacheContext, _Logger, CancellationToken.None);

                    if (dependencyInfo != null) return dependencyInfo;
                }

                return null;
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

        public async Task GetVersions(IReadOnlyDictionary<string, System.Collections.Concurrent.ConcurrentBag<NuGetVersion>> packages, IProgress<int> progress, CancellationToken? token = null)
        {
            token ??= CancellationToken.None;

            var percent = new _ProgressCounter(progress, packages.Count * _Repos.GetRepositories().Count());

            using (var cacheContext = new SourceCacheContext())
            {
                // it doesn't work, dunno why
                // Parallel.ForEach(_Repos.GetRepositories(), async repo => await _GetVersions(packages, prog, token, cacheContext, repo));                

                /* it's pretty much as slow as the plain loop below, so the API must have some bottleneck under the hood
                var tasks = _Repos.GetRepositories()
                    .Select(item => _GetVersions(packages, item, cacheContext, percent, token))
                    .ToArray();

                Task.WaitAll(tasks);
                */

                foreach (var sourceRepository in _Repos.GetRepositories())
                {
                    if (token.Value.IsCancellationRequested == true) break;

                    System.Diagnostics.Debug.WriteLine(sourceRepository.PackageSource.Source);

                    await _GetVersions(packages, sourceRepository, cacheContext, percent, token.Value);
                }
            }

            LastOperationTime = percent.Elapsed;
            RaisePropertyChanged(nameof(LastOperationTime));
        }

        private async Task _GetVersions(IReadOnlyDictionary<string, System.Collections.Concurrent.ConcurrentBag<NuGetVersion>> packages, SourceRepository sourceRepository, SourceCacheContext cacheContext, IProgress<string> progress, CancellationToken token)
        {
            var resource = await sourceRepository.GetResourceAsync<FindPackageByIdResource>();

            foreach (var package in packages)
            {
                if (token.IsCancellationRequested == true) break;

                progress.Report(package.Key);

                // if (package.Value.Count > 0) continue; // already got versions from a previous repository, so no need to look in others (NOT true, we can have overrides in local sources)

                var vvv = await resource.GetAllVersionsAsync(package.Key, cacheContext, _Logger, token);

                foreach (var v in vvv) package.Value.Add(v);
            }
        }

        #endregion
    }
}
