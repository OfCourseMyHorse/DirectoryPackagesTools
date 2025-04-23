using DirectoryPackagesTools.Client;
using NuGet.Protocol.Core.Types;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DirectoryPackagesTools
{
    public sealed class RepositoriesCollectionMVVM : KeyedViewMVVM, IEnumerable<RepositoryMVVM>
    {
        internal RepositoriesCollectionMVVM(NuGetClient client) : base("Repositories")
        {
            _Client = client;
        }

        private readonly NuGetClient _Client;

        public IEnumerator<RepositoryMVVM> GetEnumerator()
        {
            return _Client.Repositories.Select(item => new RepositoryMVVM(item)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _Client.Repositories.Select(item => new RepositoryMVVM(item)).GetEnumerator();
        }
    }

    public class RepositoryMVVM
    {
        public RepositoryMVVM(SourceRepository repository)
        {
            _Repository = repository;
        }

        private SourceRepository _Repository;

        public string Name => _Repository.PackageSource.Name;

        public string ApiKey { get; set; }

        // dotnet nuget push GraphicAssetProcessor.DotNetTool\bin\Release\GraphicAssetProcessor.DotNetTool.0.0.1-%VERSIONSUFFIX%.nupkg -s %GALLERY% -k %APIKEY% --force-english-output


    }
}
