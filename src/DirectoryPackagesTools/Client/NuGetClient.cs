using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace DirectoryPackagesTools.Client
{
    public class NuGetClient : BaseMVVM
    {
        // https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk
        // https://github.com/NuGet/Samples/blob/main/NuGetProtocolSamples/Program.cs
        // https://martinbjorkstrom.com/posts/2018-09-19-revisiting-nuget-client-libraries

        #region lifecycle

        public NuGetClient() : this((string)null) { }

        public NuGetClient(System.IO.DirectoryInfo dinfo) : this(dinfo.FullName) { }

        public NuGetClient(string root)
        {
            Settings = NuGet.Configuration.Settings.LoadDefaultSettings(root);
            var provider = new PackageSourceProvider(Settings);

            _Repos = new SourceRepositoryProvider(provider, Repository.Provider.GetCoreV3());
            Logger = NullLogger.Instance;            
        }

        #endregion

        #region data

        private SourceRepositoryProvider _Repos;        
        public ILogger Logger { get; }

        #endregion

        #region properties

        public ISettings Settings { get; }

        public IEnumerable<SourceRepository> Repositories => _Repos.GetRepositories();

        public TimeSpan LastOperationTime { get; private set; }

        #endregion

        #region API

        internal async Task ForEachRepository(Func<SourceRepositoryAPI, Task<bool>> callback, IReadOnlyCollection<string> cachedRepos = null, CancellationToken? token = null)
        {
            using var ctx = CreateContext(token ?? CancellationToken.None);

            foreach (var repo in ctx.FilterRepositories(cachedRepos ?? Array.Empty<string>()))
            {
                var result = await callback.Invoke(repo);

                if (!result) break;
            }
        }

        public NuGetClientContext CreateContext(CancellationToken? token)
        {
            token ??= CancellationToken.None;
            return new NuGetClientContext(this._Repos, this.Logger, token.Value);            
        }        

        #endregion
    }    
}
