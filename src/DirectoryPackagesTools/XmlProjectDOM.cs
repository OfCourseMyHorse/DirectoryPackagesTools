using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Wraps a .csproj general project file and exposes an API to retrieve all the PackageReference entries
    /// </summary>
    public class XmlProjectDOM
    {
        private System.Xml.Linq.XDocument _Document;

        public static IEnumerable<XmlProjectDOM> FromDirectory(System.IO.DirectoryInfo dinfo)
        {
            var allowedExtensions = new[] { ".csproj", ".targets", ".props" };

            return dinfo
                .EnumerateFiles("*", System.IO.SearchOption.AllDirectories)
                .Where(item => allowedExtensions.Any(ext=> item.Extension.ToLower().EndsWith(ext)))
                .Select(f => Load(f.FullName));            
        }

        public static XmlProjectDOM Load(string path)
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var dom = new XmlProjectDOM();
            dom._Document = doc;
            return dom;
        }

        public IEnumerable<XmlPackageReferenceVersion> GetPackageReferences()
        {
            return _Document.Root
                .Descendants(XName.Get("PackageReference"))
                .Select(item => new XmlPackageReferenceVersion(item))
                .Where(item => item.PackageId != null);
                
        }
    }
}
