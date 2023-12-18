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
    /// <remarks>
    /// Derived class: <see cref="XmlPackagesVersionsProjectDOM"/>
    /// </remarks>
    [System.Diagnostics.DebuggerDisplay("{File.Name}")]
    class XmlMSBuildProjectDOM : IPackageVersionsProject
    {
        #region factory

        public static IEnumerable<XmlMSBuildProjectDOM> EnumerateProjects(System.IO.DirectoryInfo dinfo)
        {
            return _ProjectUtils.EnumerateProjects(dinfo)
                .Select(f => Load<XmlMSBuildProjectDOM>(f.FullName));
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
            var prjs = _ProjectUtils.EnumerateProjects(dinfo)
                .Select(f => Load<XmlMSBuildProjectDOM>(f.FullName))
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
            var prjs = _ProjectUtils.EnumerateProjects(dinfo)
                .Select(f => Load<XmlMSBuildProjectDOM>(f.FullName))
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
            where T : XmlMSBuildProjectDOM, new()
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
            path ??= _Source.FullName;

            _Document.Save(path);
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
        private IPackageReferenceVersion[] _Packages => GetPackageReferences().ToArray();

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


        protected virtual string GetPackageElementName() => "PackageReference";

        public IEnumerable<IPackageReferenceVersion> GetPackageReferences()
        {
            return XmlPackageReferenceVersion.GetPackageReferences(_Document, GetPackageElementName());
        }        

        #endregion
    }
}
