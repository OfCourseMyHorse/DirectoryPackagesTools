using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Defines the source where the actual semantic version is located
    /// </summary>
    /// <remarks>
    /// Implemented by: <see cref="_AttributeVersionSource"/> and <see cref="_PropertyVersionSource"/>
    /// </remarks>
    interface IVersionSource
    {
        internal static IVersionSource _ResolveVersionSource(XElement element)
        {
            if (element == null) return null;            

            var root = element;
            while (root.Parent != null) root = root.Parent;

            var version = _AttributeVersionSource.FromVersionAttribute(element)
                ?? _PropertyVersionSource.FromVersionElement(element);

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

                version = _PropertyVersionSource.FromPropertyElement(property);
            }
        }
        public string Version { get; set; }
    }

    /// <summary>
    /// A SemVer string source located in an XML attribute
    /// </summary>
    /// <remarks>
    /// Typical Scenario:<br/>
    /// <c>
    /// PackageVersion Include="package" Version="1.0.0"
    /// </c>
    /// </remarks>
    [System.Diagnostics.DebuggerDisplay("{Version}")]
    struct _AttributeVersionSource : IVersionSource
    {
        public static IVersionSource FromVersionAttribute(XElement element)
        {
            if (element == null) return null;
            var attr = element.Attribute(XName.Get("Version"));            
            if (attr == null) return null;
            if (string.IsNullOrWhiteSpace(attr.Value)) return null;

            return new _AttributeVersionSource(attr);
        }

        private _AttributeVersionSource(XAttribute attr)
        {
            _Attribute = attr;
        }

        XAttribute _Attribute;

        public string Version
        {
            get => _Attribute.Value.Trim();
            set => _Attribute.Value = value;
        }
    }

    /// <summary>
    /// A SemVer string source located in an XML element value
    /// </summary>
    /// <remarks>
    /// Typical Scenario:<br/>
    /// <c>
    /// &lt;PropertyVersion&gt;1.0.0&lt;/PropertyVersion&gt;<br/>
    /// PackageVersion Include="package" Version="$(PropertyVersion)"
    /// </c>
    /// </remarks>
    [System.Diagnostics.DebuggerDisplay("{Version}")]
    struct _PropertyVersionSource : IVersionSource
    {
        public static IVersionSource FromVersionElement(XElement element)
        {
            if (element == null) return null;
            if (element.Name.LocalName != "Version") return null;
            if (string.IsNullOrWhiteSpace(element.Value)) return null;
            return new _PropertyVersionSource(element);
        }

        public static IVersionSource FromPropertyElement(XElement element)
        {
            if (element == null) return null;            
            if (string.IsNullOrWhiteSpace(element.Value)) return null;
            return new _PropertyVersionSource(element);
        }

        private _PropertyVersionSource(XElement element)
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
