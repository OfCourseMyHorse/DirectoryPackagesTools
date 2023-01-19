using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Wraps a Directory.Packages.Props project file and exposes an API to retrieve all the PackageVersion entries
    /// </summary>
    public class XmlPackagesVersionsProjectDOM
    {
        private System.IO.FileInfo _Source;
        private System.Xml.Linq.XDocument _Document;
        

        public static XmlPackagesVersionsProjectDOM Load(string path)
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);            

            var dom = new XmlPackagesVersionsProjectDOM();

            dom._Source = new System.IO.FileInfo(path);
            dom._Document = doc;
            return dom;
        }

        public string VerifyDocument()
        {
            var locals = this.GetPackageReferences().ToList();

            // check for duplicated entries

            var duplicated = locals
                .GroupBy(item => item.PackageId)
                .Where(item => item.Count() > 1)
                .Select(item => item.Key)
                .ToList();

            if (duplicated.Any())
            {
                var msg = string.Join(" ", duplicated);
                return $"Duplicated: {msg}";
            }

            // check cross references with projects:

            var csprojs = XmlProjectDOM.FromDirectory(_Source.Directory);

            foreach(var csproj in csprojs)
            {
                // check if a csprojs PackageReference still have Version="xxx"

                var withVersion = csproj
                    .GetPackageReferences()
                    .Where(item => !string.IsNullOrEmpty(item.Version))
                    .ToList();

                if (withVersion.Count > 0)
                {
                    var msg = string.Join("\r\n", withVersion.Select(item => item.PackageId));
                    return $"Version conflicts at {csproj.File.Name}: {msg}";
                }

                // check if a PackageReference is not in PackageVersion

                var missing = csproj
                    .GetPackageReferences()
                    .Where(item => !locals.Any(x => x.PackageId == item.PackageId))
                    .ToList();

                if (missing.Count > 0)
                {
                    var msg = string.Join("\r\n", missing.Select(item => item.PackageId));
                    return $"Version not set for: {msg}";
                }
            }

            return null;
        }


        public void Save(string path)
        {
            _Document.Save(path);
        }


        public IEnumerable<XmlPackageReferenceVersion> GetPackageReferences()
        {
            return _Document.Root
                .Descendants(XName.Get("PackageVersion"))
                .Select(item => XmlPackageReferenceVersion.From(item))
                .Where(item => item != null);
        }
    }
    
}
