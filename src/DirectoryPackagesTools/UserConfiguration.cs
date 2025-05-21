using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DirectoryPackagesTools
{
    [JsonSerializable(typeof(UserConfiguration))]
    internal partial class UserConfiguration
    {
        private static string _CfgPath => System.IO.Path.Combine(AppContext.BaseDirectory, "DirectoryPackagesTools.cfg.json");

        private static readonly Lazy<UserConfiguration> _Default = new Lazy<UserConfiguration>(()=> Load(_CfgPath));
        public static UserConfiguration Default => _Default.Value;
        public static UserConfiguration Load(string path)
        {
            if (!System.IO.Path.Exists(path)) return new UserConfiguration();

            var opts = new System.Text.Json.JsonSerializerOptions();
            opts.AllowTrailingCommas = true;
            opts.PropertyNameCaseInsensitive = true;
            opts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            var json = System.IO.File.ReadAllText(path);

            return System.Text.Json.JsonSerializer.Deserialize<UserConfiguration>(json, opts);            
        }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> SystemAuthors { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> SystemPackages { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> TestPackages { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string> HiddenPrereleasePackages { get; set; }


        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string> ObsoletePackages { get; set; }

    }
}
