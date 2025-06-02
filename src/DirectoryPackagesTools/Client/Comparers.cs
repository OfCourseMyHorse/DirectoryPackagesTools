using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace DirectoryPackagesTools.Client
{
    public class PackageSearchMetadataComparer : IComparer<IPackageSearchMetadata>
    {
        public static PackageSearchMetadataComparer Default { get; } = new PackageSearchMetadataComparer();

        public int Compare(IPackageSearchMetadata x, IPackageSearchMetadata y)
        {
            return PackageIdentityComparer.Default.Compare(x.Identity, y.Identity);
        }
    }
}
