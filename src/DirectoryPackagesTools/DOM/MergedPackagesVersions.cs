using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace DirectoryPackagesTools.DOM
{
    internal class MergedPackagesVersions : IPackageVersionsProject
    {
        #region lifecycle
        

        public static System.IO.FileInfo GetDotNetToolsFileFor(string path)
        {
            var d = new System.IO.FileInfo(path).Directory;

            while (d != null)
            {
                var p = System.IO.Path.Combine(d.FullName, ".config/dotnet-tools.json");
                var f = new System.IO.FileInfo(p);
                if (f.Exists)
                {
                    return f;                    
                }

                d = d.Parent;
            }

            return null;
        }

        public MergedPackagesVersions(XmlPackagesVersionsProjectDOM directoryPackages, JsonToolsVersionsProjectDOM jsonPackages)
        {
            _JsonPackages = jsonPackages;
            _DirectoryPackages = directoryPackages;
        }

        #endregion

        #region data

        private JsonToolsVersionsProjectDOM _JsonPackages;
        private XmlPackagesVersionsProjectDOM _DirectoryPackages;

        #endregion

        public FileInfo File => _DirectoryPackages.File;

        public IEnumerable<IPackageReferenceVersion> GetPackageReferences()
        {
            var items = _DirectoryPackages.GetPackageReferences();
            if (_JsonPackages != null) items = items.Concat(_JsonPackages.GetPackageReferences());
            return items;
        }

        public void Save(string path = null)
        {
            _DirectoryPackages.Save(path);
            _JsonPackages.Save();
        }
    }
}
