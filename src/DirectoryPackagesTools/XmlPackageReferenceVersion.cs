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

        public static XmlPackageReferenceVersion From(XElement e)
        {
            if (e == null) return null;
            var p = new XmlPackageReferenceVersion(e);
            if (string.IsNullOrWhiteSpace(p.PackageId)) return null;
            return p;
        }

        private XmlPackageReferenceVersion(XElement e)
        {
            _Element = e;
            _Version = IVersionSource._ResolveVersionSource(e);
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


    
}
