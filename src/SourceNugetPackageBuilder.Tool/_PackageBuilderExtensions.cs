using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Frameworks;
using NuGet.Packaging;

namespace SourceNugetPackageBuilder
{
    internal static class _PackageBuilderExtensions
    {
        public static void AddIcon(this PackageBuilder builder, System.IO.FileInfo iconFile)
        {
            if (iconFile == null || !iconFile.Exists) return;

            builder.Icon = iconFile.Name;
            var pkgFile = new PhysicalPackageFile();
            pkgFile.SourcePath = iconFile.FullName;
            pkgFile.TargetPath = iconFile.Name;
            builder.Files.Add(pkgFile);
        }        

        public static string GetSanitizedFrameworkMoniker(this NuGetFramework tfm)
        {
            var fw = tfm.GetShortFolderName();

            switch (fw) // append missing API level
            {
                case "net6.0-android": fw += "31.0"; break;
                case "net7.0-android": fw += "33.0"; break;
                case "net8.0-android": fw += "34.0"; break;

                case "unsupported": throw new ArgumentException("unsupported", nameof(fw));
            }

            return fw;
        }
    }
}
