using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using NuGet.Versioning;

namespace DirectoryPackagesTools.DOM
{
    /// <summary>
    /// Wraps a .csproj general project file and exposes an API to retrieve all the PackageReference entries
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{File.Name}")]
    class XmlProjectDOM
    {
        #region factory

        public static IEnumerable<XmlProjectDOM> EnumerateProjects(System.IO.DirectoryInfo dinfo, bool excludeDirPackProps = true)
        {
            return _EnumerateProjects(dinfo, excludeDirPackProps)
                .Select(f => Load<XmlProjectDOM>(f.FullName));
        }        

        private static IEnumerable<System.IO.FileInfo> _EnumerateProjects(System.IO.DirectoryInfo dinfo, bool excludeDirPackProps = true)
        {
            if (dinfo.LinkTarget != null)
            {
                return Enumerable.Empty<System.IO.FileInfo>();
            }

            var allowedExtensions = new[] { ".csproj", ".targets", ".props" };

            var files = dinfo
                .EnumerateFiles()
                .Where(item => !_IsWorkDir(item.Directory))
                .Where(item => allowedExtensions.Any(ext => item.Extension.ToLower().EndsWith(ext)))
                .Where(item => !excludeDirPackProps || item.Name.ToLower() != "directory.packages.props");

            var dfiles = dinfo
                .EnumerateDirectories()
                .Where(item => !_ContainsPackagesOverride(item) ) // don't look into subdirectories containing a packages override
                .SelectMany(item => _EnumerateProjects(item, excludeDirPackProps));

            return files.Concat(dfiles);
        }

        private static bool _IsWorkDir(System.IO.DirectoryInfo dinfo)
        {
            while (dinfo != null)
            {
                var name = dinfo.Name.ToLower();
                if (name == "bin" || name == "obj") return true;
                dinfo = dinfo.Parent;
            }

            return false;
        }

        private static bool _ContainsPackagesOverride(System.IO.DirectoryInfo dinfo)
        {
            if (dinfo.LinkTarget != null) return false;

            return dinfo.EnumerateFiles().Any(item => item.Name.ToLower() == "directory.packages.props");
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

            foreach (var prj in prjs)
            {
                bool modified = false;

                foreach (var pkg in prj.GetPackageReferences())
                {
                    pkg.RemoveVersion();

                    modified = true;
                }

                if (modified) prj.Save();
            }
        }


        public static void RestoreVersionsToProjectsFiles(System.IO.DirectoryInfo dinfo, IReadOnlyDictionary<string, string> packageVersions)
        {
            var prjs = _EnumerateProjects(dinfo, true)
                .Select(f => Load<XmlProjectDOM>(f.FullName))
                .Where(prj => prj.ManagePackageVersionsCentrally);

            foreach (var prj in prjs)
            {
                bool modified = false;

                foreach (var pkg in prj.GetPackageReferences())
                {
                    pkg.SetVersion(packageVersions);

                    modified = true;
                }

                if (modified) prj.Save();
            }
        }


        public static T Load<T>(string path)
            where T : XmlProjectDOM, new()
        {
            try
            {
                var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);

                var dom = new T();
                dom._Source = new System.IO.FileInfo(path);
                dom._Document = doc;
                return dom;
            }
            catch (Exception ex)
            {
                throw new System.IO.FileLoadException($"Failed to load {path}", ex);
            }
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
        private XDocument _Document;

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
