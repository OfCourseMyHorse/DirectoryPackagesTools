using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Versioning;

using KVPMACRO = System.Collections.Generic.KeyValuePair<string, string>;

namespace DirectoryPackagesTools.DOM
{
    /// <summary>
    /// Wraps an <see cref="XElement"/> representing a PackageReference or a PackageVersion item.
    /// </summary>

    [System.Diagnostics.DebuggerDisplay("{PackageId} {Version}")]
    sealed class XmlPackageReferenceVersion : IPackageReferenceVersion
    {
        #region lifecycle

        public static IEnumerable<XmlPackageReferenceVersion> GetPackageReferences(XDocument doc, string itemName)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentNullException(nameof(itemName));

            if (itemName != "PackageVersion" && itemName != "PackageReference") throw new ArgumentException("Must be a valid element name", nameof(itemName));

            var xName = doc.Root.GetDefaultNamespace().GetName(itemName);

            return doc.Root
                .Descendants(xName)                
                .SelectMany(item => _From(item))
                .Where(item => item != null)
                .ToList();
        }

        private static IEnumerable<XmlPackageReferenceVersion> _From(XElement e)
        {
            if (e == null) yield break;
            var p = new XmlPackageReferenceVersion(e);
            if (string.IsNullOrWhiteSpace(p.PackageId)) yield break;

            // update elements are expected to have another include element before it.
            if (p.IsUpdate) { yield break; }
            
            // not a macro, so we return the element as is.
            if (!p.PackageId.Contains("$(")) { yield return p; yield break; }            

            // Some packages have a dual PackageId.Debug and PackageId.Release variants like Avalonia.Diagnostics
            if (p.PackageId.Contains("$(Configuration)"))
            {
                yield return new XmlPackageReferenceVersion(e, new KVPMACRO("$(Configuration)", "Debug"));
                yield return new XmlPackageReferenceVersion(e, new KVPMACRO("$(Configuration)", "Release"));
            }

            // for other macros, do nothing
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

        public bool IsUpdate => _Element.Attribute(XName.Get("Update"))?.Value != null;

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

        public VersionRange Version
        {
            get => _Version?.Version == null ? null : VersionRange.Parse(_Version.Version);
            set => _Version.Version = value.ToShortString();
        }

        #endregion

        #region API

        public void RemoveVersion()
        {
            IVersionXmlSource._RemoveVersion(_Element);
        }

        public void SetVersion(IReadOnlyDictionary<string, string> packageVersions)
        {
            if (packageVersions.TryGetValue(this.PackageId, out var semver))
            {
                IVersionXmlSource._SetVersion(_Element, semver);
            }            
        }

        #endregion
    }



}
