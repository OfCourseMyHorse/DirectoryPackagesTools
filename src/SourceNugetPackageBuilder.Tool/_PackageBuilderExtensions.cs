using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    }
}
