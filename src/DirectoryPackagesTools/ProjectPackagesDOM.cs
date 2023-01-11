using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Represents a .csproj file and exposes an API to retrieve all the PackageReferences
    /// </summary>
    public class ProjectPackagesDOM
    {
        private System.Xml.Linq.XDocument _Document;

        public static IEnumerable<ProjectPackagesDOM> FromDirectory(System.IO.DirectoryInfo dinfo)
        {
            var allowedExtensions = new[] { ".csproj", ".targets", ".props" };

            return dinfo
                .EnumerateFiles("*", System.IO.SearchOption.AllDirectories)
                .Where(item => allowedExtensions.Any(ext=> item.Extension.ToLower().EndsWith(ext)))
                .Select(f => Load(f.FullName));            
        }

        public static ProjectPackagesDOM Load(string path)
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var dom = new ProjectPackagesDOM();
            dom._Document = doc;
            return dom;
        }

        public IEnumerable<PackageReferenceVersion> GetPackageReferences()
        {
            return _Document.Root
                .Descendants(XName.Get("PackageReference"))
                .Select(item => new PackageReferenceVersion(item));
        }
    }
}
