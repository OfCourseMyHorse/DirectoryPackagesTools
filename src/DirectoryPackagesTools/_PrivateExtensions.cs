using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    internal static class _PrivateExtensions
    {
        public static bool IsPackageNameContainedInFilters(this string packageIdentity, IEnumerable<string> filters)
        {
            if (filters == null) return false;
            
            foreach (var filter in filters)
            {
                if (filter.EndsWith('*') && packageIdentity.StartsWith(filter.Substring(0, filter.Length - 1), StringComparison.OrdinalIgnoreCase)) return true;
                if (packageIdentity.Equals(filter, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
            
        }
    }
}
