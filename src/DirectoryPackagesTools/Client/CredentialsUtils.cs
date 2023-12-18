using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Configuration;

namespace DirectoryPackagesTools.Client
{
    public static class CredentialsUtils
    {
        public static bool EncryptNugetConfigClearTextPasswords(System.IO.FileInfo fileName)
        {
            // Load the settings from a specific file
            var settings = NuGet.Configuration.Settings.LoadSpecificSettings(fileName.Directory.FullName, fileName.Name);

            // Get the packageSources section
            var packageSourceCreds = settings.GetSection("packageSourceCredentials");

            var items = packageSourceCreds
                .Items
                .OfType<NuGet.Configuration.CredentialsItem>()
                .Where(item => item.IsPasswordClearText)
                .ToList();

            if (!items.Any()) return false;

            foreach (var item in items)
            {
                var xpass = EncryptionUtility.EncryptString(item.Password);
                item.UpdatePassword(xpass, false);
                settings.AddOrUpdate("packageSourceCredentials", item);
            }

            // Save the settings to the same file
            settings.SaveToDisk();

            return true;
        }
    }
}
