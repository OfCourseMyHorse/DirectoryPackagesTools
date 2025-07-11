using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DirectoryPackagesTools.Utils
{
    /// <summary>
    /// Used by <see cref="PackageClassifier"/>
    /// </summary>
    [JsonSerializable(typeof(UserConfiguration))]
    internal partial class UserConfiguration
    {
        #region singleton
        private static string _CfgPath => System.IO.Path.Combine(AppContext.BaseDirectory, "DirectoryPackagesTools.cfg.json");

        private static readonly Lazy<UserConfiguration> _Default = new Lazy<UserConfiguration>(()=> Load(_CfgPath));
        public static UserConfiguration Default => _Default.Value;

        #endregion

        #region serialization
        public static UserConfiguration Load(string path)
        {
            if (!System.IO.Path.Exists(path)) return new UserConfiguration();

            var opts = new JsonSerializerOptions();
            opts.AllowTrailingCommas = true;
            opts.PropertyNameCaseInsensitive = true;
            opts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            var json = System.IO.File.ReadAllText(path);

            return JsonSerializer.Deserialize<UserConfiguration>(json, opts);            
        }

        #endregion

        #region properties

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
        public List<string> ShowUnlistedFromAuthors { get; set; } // not implemented yet

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> SystemAuthors { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> SystemPackages { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> TestPackages { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> HiddenPrereleasePackages { get; set; }


        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> ObsoletePackages { get; set; }

        #endregion

    }
}
