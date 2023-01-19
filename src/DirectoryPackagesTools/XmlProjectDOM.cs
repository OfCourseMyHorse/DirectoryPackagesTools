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
    [System.Diagnostics.DebuggerDisplay("{File.Name}")]
    public class XmlProjectDOM
    {
        #region factory

        public static IEnumerable<XmlProjectDOM> FromDirectory(System.IO.DirectoryInfo dinfo, bool excludeDirPackProps = true)
        {
            var allowedExtensions = new[] { ".csproj", ".targets", ".props" };

            return dinfo
                .EnumerateFiles("*", System.IO.SearchOption.AllDirectories)
                .Where(item => allowedExtensions.Any(ext => item.Extension.ToLower().EndsWith(ext)))
                .Where(item => !excludeDirPackProps || item.Name.ToLower() != "directory.packages.props")
                .Select(f => Load(f.FullName));
        }

        public static XmlProjectDOM Load(string path)
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var dom = new XmlProjectDOM();
            dom._Source = new System.IO.FileInfo(path);
            dom._Document = doc;
            return dom;
        }

        #endregion

        #region data

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private System.IO.FileInfo _Source;
        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private System.Xml.Linq.XDocument _Document;

        #endregion

        #region properties

        public System.IO.FileInfo File => _Source;

        #if DEBUG

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.RootHidden)]
        private XmlPackageReferenceVersion[] _Packages => GetPackageReferences().ToArray();

        #endif

        #endregion

        #region API

        public IEnumerable<XmlPackageReferenceVersion> GetPackageReferences()
        {
            return _Document.Root
                .Descendants(XName.Get("PackageReference"))
                .Select(item => XmlPackageReferenceVersion.From(item))
                .Where(item => item != null);                
        }

        #endregion
    }
}
