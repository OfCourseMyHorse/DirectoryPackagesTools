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

        private static IEnumerable<System.IO.FileInfo> _EnumerateProjects(System.IO.DirectoryInfo dinfo, bool excludeDirPackProps = true)
        {
            var allowedExtensions = new[] { ".csproj", ".targets", ".props" };

            return dinfo
                .EnumerateFiles("*", System.IO.SearchOption.AllDirectories)
                .Where(item => allowedExtensions.Any(ext => item.Extension.ToLower().EndsWith(ext)))
                .Where(item => !excludeDirPackProps || item.Name.ToLower() != "directory.packages.props");
        }

        public static IEnumerable<XmlProjectDOM> FromDirectory(System.IO.DirectoryInfo dinfo, bool excludeDirPackProps = true)
        {
            return _EnumerateProjects(dinfo,excludeDirPackProps).Select(f => Load<XmlProjectDOM>(f.FullName));
        }

        /// <summary>
        /// Iterates over all the csproj, targets and props of a directory and removes all the Version entries of PackageReference items.
        /// </summary>
        /// <remarks>
        /// This method is quite destructive, it should be called only AFTER executing <see cref="XmlPackagesVersionsProjectDOM.CreateVersionFileFromExistingProjects(System.IO.FileInfo)"/>
        /// </remarks>
        /// <param name="dinfo">the target directory</param>
        public static void RemoveVersionsFromProjectsFiles(System.IO.DirectoryInfo dinfo)
        {
            var prjs = _EnumerateProjects(dinfo, true)
                .Select(f => Load<XmlProjectDOM>(f.FullName))
                .Where(prj => prj.ManagePackageVersionsCentrally);

            foreach(var prj in prjs)
            {
                bool modified = false;

                foreach(var pkg in prj.GetPackageReferences())
                {
                    pkg.RemoveVersion();

                    modified = true;
                }

                if (modified) prj.Save();
            }

        }

        public static T Load<T>(string path)
            where T: XmlProjectDOM, new()
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var dom = new T();
            dom._Source = new System.IO.FileInfo(path);
            dom._Document = doc;
            return dom;
        }

        public void Save(string path = null)
        {
            _Document.Save(path ?? _Source.FullName);
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

        public bool ManagePackageVersionsCentrally
        {
            get
            {
                var value = GetPropertyValue("ManagePackageVersionsCentrally")?.ToLower()?.Trim();

                return "true" == (value ?? "true");
            }
        }

        public string GetPropertyValue(string propertyName)
        {
            return _Document
                    .Descendants(XName.Get("PropertyGroup")).SelectMany(item => item.Descendants())
                    .FirstOrDefault(item => item.Name.LocalName == propertyName)
                    ?.Value;
        }

        public virtual IEnumerable<XmlPackageReferenceVersion> GetPackageReferences(string itemName = "PackageReference")
        {
            return XmlPackageReferenceVersion.GetPackageReferences(_Document, itemName);
        }

        #endregion
    }
}
