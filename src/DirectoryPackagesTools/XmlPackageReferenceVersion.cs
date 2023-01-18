using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Wraps an <see cref="XElement"/> object representing a PackageReference or PackageVersion object
    /// </summary>

    [System.Diagnostics.DebuggerDisplay("{PackageId} {Version}")]
    public sealed class XmlPackageReferenceVersion
    {
        #region lifecycle

        internal XmlPackageReferenceVersion(XElement e)
        {
            _Element = e;
            _Version = e._ResolveVersionSource();
        }

        #endregion

        #region data

        private readonly XElement _Element;
        private readonly IVersionSource _Version;

        #endregion

        #region properties

        public string PackageId
        {
            get
            {
                var attr
                    = _Element.Attribute(XName.Get("Include"))
                    ?? _Element.Attribute(XName.Get("Update"));

                return attr?.Value ?? null;
            }
        }

        public string PackagePrefix => new string(PackageId.TakeWhile(c => c != '.').ToArray());

        public bool HasVersionRange
        {
            get
            {
                // https://github.com/NuGet/Home/issues/6763#issuecomment-633465943
                return _Version.Version.StartsWith("[") && _Version.Version.EndsWith("]");
            }
        }

        public string Version
        {
            get => _Version?.Version?.Trim('[', ']') ?? null;
            set => _Version.Version = HasVersionRange ? "[" + value + "]" : value;
        }

        #endregion
    }


    /// <summary>
    /// Defines the source where the actual version is located
    /// </summary>
    /// <remarks>
    /// Implemented by: <see cref="_AttributeVersionSource"/> and <see cref="_PropertyVersionSource"/>
    /// </remarks>
    interface IVersionSource
    {
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
        public static IVersionSource CreateFrom(XElement element)
        {
            if (element == null) return null;
            var attr = element.Attribute(XName.Get("Version"));
            if (attr == null) return null;

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
        public _PropertyVersionSource(XElement element)
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
