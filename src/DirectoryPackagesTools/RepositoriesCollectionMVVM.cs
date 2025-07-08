using DirectoryPackagesTools.Client;

using NuGet.Configuration;
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
        #region lifecycle

        internal RepositoriesCollectionMVVM(NuGetClient client) : base("Repositories")
        {
            _Client = client;
            _Context = _Client.CreateContext(CancellationToken.None);

            Credentials = _Client.Settings.GetSection("packageSourceCredentials").Items.OfType<CredentialsItem>().ToList();
            ApiKeys = _Client.Settings.GetSection("apikeys").Items.OfType<AddItem>().ToList();
        }

        #endregion

        #region properties

        private readonly NuGetClient _Client;
        private readonly NuGetClientContext _Context;
        public IReadOnlyList<CredentialsItem> Credentials { get; }
        public IReadOnlyList<AddItem> ApiKeys { get; }

        #endregion

        #region API

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<RepositoryMVVM> GetEnumerator()
        {
            return _Client.Repositories.Select(item => new RepositoryMVVM(item, _Context, Credentials, ApiKeys)).GetEnumerator();
        }        

        #endregion
    }


}
