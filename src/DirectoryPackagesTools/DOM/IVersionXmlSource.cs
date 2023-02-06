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
    /// Defines the source where the actual semantic version is located, within an XML document.
    /// </summary>
    /// <remarks>
    /// Implemented by: <see cref="_AttributeVersionXmlSource"/> and <see cref="_PropertyVersionXmlSource"/>
    /// </remarks>
    interface IVersionXmlSource
    {
        internal static void _RemoveVersion(XElement element)
        {
            if (element == null) return;

            var vName = XName.Get("Version");

            element.Attribute(vName)?.Remove();
            element.Element(vName)?.Remove();
        }

        internal static void _SetVersion(XElement element, string semver)
        {
            if (element == null) return;

            var vName = XName.Get("Version");

            element.SetAttributeValue(vName, semver);
        }

        internal static IVersionXmlSource _ResolveVersionSource(XElement element)
        {
            if (element == null) return null;

            var root = element;
            while (root.Parent != null) root = root.Parent;

            var version = _AttributeVersionXmlSource.FromVersionAttribute(element)
                ?? _PropertyVersionXmlSource.FromVersionElement(element);

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

                version = _PropertyVersionXmlSource.FromPropertyElement(property);
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
    struct _AttributeVersionXmlSource : IVersionXmlSource
    {
        public static IVersionXmlSource FromVersionAttribute(XElement element)
        {
            if (element == null) return null;
            var attr = element.Attribute(XName.Get("Version"));
            if (attr == null) return null;
            if (string.IsNullOrWhiteSpace(attr.Value)) return null;

            return new _AttributeVersionXmlSource(attr);
        }

        private _AttributeVersionXmlSource(XAttribute attr)
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
    struct _PropertyVersionXmlSource : IVersionXmlSource
    {
        public static IVersionXmlSource FromVersionElement(XElement element)
        {
            if (element == null) return null;

            System.Diagnostics.Debug.Assert(element.Name.LocalName.StartsWith("Package")); // PackageReference | PackageVersion

            element = element.Element(XName.Get("Version"));
            if (element == null) return null;
            if (element.Name.LocalName != "Version") return null;
            if (string.IsNullOrWhiteSpace(element.Value)) return null;
            return new _PropertyVersionXmlSource(element);
        }

        public static IVersionXmlSource FromPropertyElement(XElement element)
        {
            if (element == null) return null;
            if (string.IsNullOrWhiteSpace(element.Value)) return null;
            return new _PropertyVersionXmlSource(element);
        }

        private _PropertyVersionXmlSource(XElement element)
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
