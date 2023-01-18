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
    public class XmlPropsProjectDOM
    {
        private System.Xml.Linq.XDocument _Document;

        public static XmlPropsProjectDOM Load(string path)
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var dom = new XmlPropsProjectDOM();
            dom._Document = doc;
            return dom;
        }

        public string VerifyDocument()
        {
            var locals = this.GetPackageReferences().ToList();

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
                .Select(item => new XmlPackageReferenceVersion(item))
                .Where(item => item.PackageId != null);
        }
    }
    
}
