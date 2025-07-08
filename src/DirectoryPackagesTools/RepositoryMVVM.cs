using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DirectoryPackagesTools.Client;

using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Represents a Nuget Gallery repository.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{Name}")]
    public class RepositoryMVVM
    {
        #region lifecycle
        public RepositoryMVVM(SourceRepository repository, NuGetClientContext client, IEnumerable<CredentialsItem> credentials, IEnumerable<AddItem> apiKeys)
        {
            _Repository = repository;

            _RepoAPI = client.Repositories.FirstOrDefault(item => item.Source == repository);

            if (credentials != null)
            {
                Credentials = credentials.FirstOrDefault(item => item.ElementName == repository.PackageSource.Name);
            }

            var srcUri = repository.PackageSource.SourceUri;

            if (apiKeys != null && srcUri != null && !srcUri.IsFile)
            {
                ApiKey = apiKeys.FirstOrDefault(item => item.Key.Contains(srcUri.Host));
            }
        }

        #endregion

        #region data

        private NuGetClientContext _Client;
        private SourceRepository _Repository;
        private SourceRepositoryAPI _RepoAPI;

        public CredentialsItem Credentials { get; }

        public AddItem ApiKey { get; }

        public string CredentailsClearPassword
        {
            get
            {
                var pw = Credentials?.Password;
                if (string.IsNullOrWhiteSpace(pw)) return "not set";

                if (Credentials.IsPasswordClearText) return pw;

                return EncryptionUtility.DecryptString(pw);
            }
                

        }

        #endregion

        #region Properties

        public string Name => _Repository.PackageSource.Name;
        

        public Task<IReadOnlyList<IPackageSearchMetadata>> PackagesAsync
        {
            get
            {
                if (_RepoAPI.IsNugetOrg || _RepoAPI.IsVisualStudio || _RepoAPI.IsOfficial) return Task.FromResult<IReadOnlyList<IPackageSearchMetadata>>(Array.Empty<IPackageSearchMetadata>());

                return _RepoAPI.SearchAsync(new SearchFilter(true));
            }
        }

        #endregion

        #region API

        // dotnet nuget push GraphicAssetProcessor.DotNetTool\bin\Release\GraphicAssetProcessor.DotNetTool.0.0.1-%VERSIONSUFFIX%.nupkg -s %GALLERY% -k %APIKEY% --force-english-output

        #endregion
    }
}
