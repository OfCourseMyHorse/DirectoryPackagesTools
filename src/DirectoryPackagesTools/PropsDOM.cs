using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    public class PropsDOM
    {
        private System.Xml.Linq.XDocument _Document;

        public static PropsDOM Load(string path)
        {
            var doc = System.Xml.Linq.XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);

            var dom = new PropsDOM();
            dom._Document = doc;
            return dom;
        }


        public void Save(string path)
        {
            _Document.Save(path);
        }


        public IEnumerable<PackageReferenceVersion> GetPackageReferences()
        {
            return _Document.Root.Descendants(XName.Get("PackageVersion")).Select(item => new PackageReferenceVersion(item, _ResolveVersion(item)));
        }

        private IVersion _ResolveVersion(XElement element)
        {
            var version = new _AttributeVersion(element) as IVersion;

            while (true) // recursively resolve
            {
                if (!version.Version.Contains("$")) return version;

                var propName = version.Version;
                propName = propName.TrimStart('$', '(');
                propName = propName.TrimEnd(')');                

                var properties = _Document.Root
                    .Descendants(XName.Get("PropertyGroup")).SelectMany(item => item.Descendants())
                    .ToList();

                var property = properties.FirstOrDefault(item => item.Name.LocalName == propName);

                if (property == null) throw new InvalidOperationException($"element {propName} not found");

                version = new _PropertyVersion(property);
            }
        }
    }


    public sealed class PackageReferenceVersion
    {
        internal PackageReferenceVersion(XElement e, IVersion v) { _Element = e; _Version = v; }

        private readonly XElement _Element;
        private readonly IVersion _Version;

        public string PackageId => _Element.Attribute(XName.Get("Include")).Value;

        public string Version
        {
            get => _Version.Version;
            set => _Version.Version = value;
        }
    }


    interface IVersion
    {
        public string Version { get; set; }
    }

    struct _AttributeVersion : IVersion
    {
        public _AttributeVersion(XElement element)
        {
            _Attribute = element.Attribute(XName.Get("Version"));
        }

        XAttribute _Attribute;

        public string Version
        {
            get => _Attribute.Value.Trim();
            set => _Attribute.Value = value;
        }
    }

    struct _PropertyVersion : IVersion
    {
        public _PropertyVersion(XElement element)
        {
            _Property = element;
        }

        XElement _Property;

        public string Version
        {
            get => _Property.Value.Trim();
            set => _Property.Value = value;
        }
    }
}
