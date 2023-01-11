using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    static class _PrivateExtensions
    {
        public static IVersionSource _ResolveVersionSource(this XElement element)
        {
            var root = element;
            while (root.Parent != null) root = root.Parent;            

            var version = _AttributeVersionSource.CreateFrom(element);
            if (version == null) return null;            

            while (true) // recursively resolve
            {
                if (!version.Version.Contains("$")) return version;

                var propName = version.Version;
                propName = propName.TrimStart('$', '(');
                propName = propName.TrimEnd(')');

                var properties = root
                    .Descendants(XName.Get("PropertyGroup")).SelectMany(item => item.Descendants())
                    .ToList();

                var property = properties.FirstOrDefault(item => item.Name.LocalName == propName);

                if (property == null) throw new InvalidOperationException($"element {propName} not found");

                version = new _PropertyVersionSource(property);
            }
        }
    }
}
