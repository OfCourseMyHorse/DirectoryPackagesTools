using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Versioning;

namespace DirectoryPackagesTools.DOM
{
    [System.Diagnostics.DebuggerDisplay("{File}")]
    internal class JsonToolsVersionsProjectDOM : IPackageVersionsProject
    {
        #region lifecycle

        public static JsonToolsVersionsProjectDOM Load(string filePath)
        {
            var finfo = new System.IO.FileInfo(filePath);

            var json = System.IO.File.ReadAllText(filePath);
            var dom = System.Text.Json.Nodes.JsonNode.Parse(json);

            return new JsonToolsVersionsProjectDOM(finfo, dom);
        }        

        private JsonToolsVersionsProjectDOM(System.IO.FileInfo file, System.Text.Json.Nodes.JsonNode dom)
        {
            File = file;
            _DOM = dom;
        }

        #endregion

        #region data

        public System.IO.FileInfo File { get; }

        private readonly System.Text.Json.Nodes.JsonNode _DOM;

        #endregion

        #region API

        public IEnumerable<IPackageReferenceVersion> GetPackageReferences()
        {
            var toolsItem = _DOM.AsObject()["tools"].AsObject();

            foreach (KeyValuePair<string,System.Text.Json.Nodes.JsonNode> entry in toolsItem)
            {
                yield return new JsonPackageReferenceVersion(entry.Key, entry.Value);
            }
        }

        public void Save(string path = null)
        {
            var opts = new System.Text.Json.JsonSerializerOptions();
            opts.WriteIndented = true;
            
            var json = _DOM.ToJsonString(opts);

            path ??= File.FullName;

            System.IO.File.WriteAllText(path, json);
        }

        #endregion
    }

    [System.Diagnostics.DebuggerDisplay("{PackageId} {Version}")]
    internal struct JsonPackageReferenceVersion : IPackageReferenceVersion
    {
        #region lifecycle
        public JsonPackageReferenceVersion(string packageId, System.Text.Json.Nodes.JsonNode node)
        {
            this.PackageId = packageId;            
            this._Props = node.AsObject();
        }

        #endregion

        #region data

        private System.Text.Json.Nodes.JsonObject _Props;

        public string PackageId { get; }

        #endregion

        #region properties

        public VersionRange Version
        {
            get => VersionRange.Parse(_Props["version"].GetValue<string>());
            set => _Props["version"] = value.ToShortString();
        }

        #endregion

        #region API

        public void RemoveVersion()
        {
            // do nothing
        }

        public void SetVersion(IReadOnlyDictionary<string, string> packageVersions)
        {
            // do nothing
        }

        #endregion
    }
}
