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

            var credsSection = _Client.Settings.GetSection("packageSourceCredentials");
            Credentials = credsSection != null ? credsSection.Items.OfType<CredentialsItem>().ToList() : null;

            var apikSection = _Client.Settings.GetSection("packageSourceCredentials");
            ApiKeys = apikSection != null ? apikSection.Items.OfType<AddItem>().ToList() : null;
        }

        #endregion

        #region properties

        private readonly NuGetClient _Client;        
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
            return _Client.Repositories.Select(item => new RepositoryMVVM(item.Source, _Client, Credentials, ApiKeys)).GetEnumerator();
        }        

        #endregion
    }


}
