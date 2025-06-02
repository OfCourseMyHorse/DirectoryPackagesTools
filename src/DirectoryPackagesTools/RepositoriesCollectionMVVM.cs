using DirectoryPackagesTools.Client;
using NuGet.Protocol.Core.Types;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DirectoryPackagesTools
{
    public sealed class RepositoriesCollectionMVVM : KeyedViewMVVM, IEnumerable<RepositoryMVVM>
    {
        internal RepositoriesCollectionMVVM(NuGetClient client) : base("Repositories")
        {
            _Client = client;
            _Context = _Client.CreateContext(CancellationToken.None);
        }

        private readonly NuGetClient _Client;
        private readonly NuGetClientContext _Context;

        public IEnumerator<RepositoryMVVM> GetEnumerator()
        {
            return _Client.Repositories.Select(item => new RepositoryMVVM(item, _Context)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Client.Repositories.Select(item => new RepositoryMVVM(item, _Context)).GetEnumerator();
        }
    }

    public class RepositoryMVVM
    {
        #region lifecycle
        public RepositoryMVVM(SourceRepository repository, NuGetClientContext client)
        {
            _Repository = repository;

            _RepoAPI = client.Repositories.FirstOrDefault(item => item.Source == repository);
        }

        #endregion

        #region data

        private NuGetClientContext _Client;
        private SourceRepository _Repository;
        private SourceRepositoryAPI _RepoAPI;

        #endregion

        #region Properties

        public string Name => _Repository.PackageSource.Name;

        public string ApiKey { get; set; }

        public Task<IReadOnlyList<IPackageSearchMetadata>> PackagesAsync
        {
            get
            {
                if (_RepoAPI.IsNugetOrg || _RepoAPI.IsVisualStudio || _RepoAPI.IsOfficial) return Task.FromResult<IReadOnlyList<IPackageSearchMetadata>>(Array.Empty<IPackageSearchMetadata>());

                return _RepoAPI.SearchAsync(new SearchFilter(true));
            }
        }

        #endregion


        // dotnet nuget push GraphicAssetProcessor.DotNetTool\bin\Release\GraphicAssetProcessor.DotNetTool.0.0.1-%VERSIONSUFFIX%.nupkg -s %GALLERY% -k %APIKEY% --force-english-output


    }
}
