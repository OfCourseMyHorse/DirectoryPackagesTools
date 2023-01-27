using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using KVPMACRO = System.Collections.Generic.KeyValuePair<string, string>;

namespace DirectoryPackagesTools.DOM
{
    /// <summary>
    /// Wraps an <see cref="XElement"/> representing a PackageReference or a PackageVersion item.
    /// </summary>

    [System.Diagnostics.DebuggerDisplay("{PackageId} {Version}")]
    sealed class XmlPackageReferenceVersion
    {
        #region lifecycle

        public static IEnumerable<XmlPackageReferenceVersion> GetPackageReferences(XDocument doc, string itemName)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentNullException(nameof(itemName));

            if (itemName != "PackageVersion" && itemName != "PackageReference") throw new ArgumentException("Must be a valid element name", nameof(itemName));

            return doc.Root
                .Descendants(XName.Get(itemName))
                .SelectMany(item => _From(item))
                .Where(item => item != null)
                .ToList();
        }

        private static IEnumerable<XmlPackageReferenceVersion> _From(XElement e)
        {
            if (e == null) yield break;
            var p = new XmlPackageReferenceVersion(e);
            if (string.IsNullOrWhiteSpace(p.PackageId)) yield break;

            if (!p.PackageId.Contains("$("))
            {
                yield return p;
                yield break;
            }

            if (p.PackageId.Contains("$(Configuration)"))
            {
                yield return new XmlPackageReferenceVersion(e, new KVPMACRO("$(Configuration)", "Debug"));
                yield return new XmlPackageReferenceVersion(e, new KVPMACRO("$(Configuration)", "Release"));
            }
        }

        private XmlPackageReferenceVersion(XElement e, params KVPMACRO[] macros)
        {
            _Element = e;
            _Version = IVersionXmlSource._ResolveVersionSource(e);

            if (macros.Length > 0)
            {
                _NameMacros = new Dictionary<string, string>(macros);
            }
        }

        #endregion

        #region data

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private readonly XElement _Element;

        [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
        private readonly IVersionXmlSource _Version;

        private readonly Dictionary<string, string> _NameMacros;

        #endregion

        #region properties

        public string PackageId
        {
            get
            {
                var attr
                    = _Element.Attribute(XName.Get("Include"))
                    ?? _Element.Attribute(XName.Get("Update"));

                var name = attr?.Value ?? null;

                if (_NameMacros != null)
                {
                    foreach (var kvp in _NameMacros)
                    {
                        name = name.Replace(kvp.Key, kvp.Value);
                    }
                }

                return name;
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

        #region API

        public void RemoveVersion()
        {
            IVersionXmlSource._RemoveVersion(_Element);
        }

        #endregion
    }



}
