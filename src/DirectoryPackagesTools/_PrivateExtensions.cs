using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using NuGet.Packaging;

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

        /// <summary>
        /// Retrieves the frameworks used by the package
        /// </summary>
        /// <remarks>
        /// still unable to retrieve frameworks when app is a tool.
        /// </remarks>
        /// <param name="packageArchive">the package to read</param>
        /// <returns>the list of frameworks</returns>        
        public static IEnumerable<string> GetFrameworks(this PackageArchiveReader packageArchive)
        {
            if (packageArchive == null) return Enumerable.Empty<string>();

            // Get the groups of reference items (which are grouped by TFM)
            var frameworkSpecificGroups = packageArchive.GetReferenceItems()
                                                .Select(item => item.TargetFramework)
                                                .Distinct()
                                                .ToList();

            // If no reference items are found, check content items, though lib/ref is the primary source
            if (!frameworkSpecificGroups.Any())
            {
                frameworkSpecificGroups = packageArchive.GetContentItems()
                                                .Select(item => item.TargetFramework)
                                                .Distinct()
                                                .ToList();
            }

            if (!frameworkSpecificGroups.Any())
            {
                frameworkSpecificGroups = packageArchive.GetToolItems()
                                                .Select(item => item.TargetFramework)
                                                .Distinct()
                                                .ToList();
            }

            // Convert TFMs to a readable format
            var frameworks = frameworkSpecificGroups
                .Where(tf => !tf.IsUnsupported) // Filter out the "any" framework or unsupported ones
                .Select(tf => tf.GetFrameworkString())
                .Distinct();

            return frameworks;
        }
    }
}
