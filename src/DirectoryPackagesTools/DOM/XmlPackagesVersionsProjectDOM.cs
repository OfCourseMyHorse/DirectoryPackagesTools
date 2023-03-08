using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools.DOM
{
    /// <summary>
    /// Wraps a Directory.Packages.Props project file and exposes an API to retrieve all the PackageVersion entries
    /// </summary>
    class XmlPackagesVersionsProjectDOM : XmlProjectDOM
    {
        public static void CreateVersionFileFromExistingProjects(System.IO.FileInfo finfo)
        {
            var packages =
                EnumerateProjects(finfo.Directory)
                .Where(prj => prj.ManagePackageVersionsCentrally)
                .SelectMany(item => item.GetPackageReferences())
                .GroupBy(item => item.PackageId)
                .OrderBy(item => item.Key);

            NuGet.Versioning.VersionRange getVersionFrom(IEnumerable<XmlPackageReferenceVersion> references)
            {
                references = references.Where(item => item.Version != null);
                if (!references.Any()) return NuGet.Versioning.VersionRange.None;

                return references.First().Version;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Project>");

            sb.AppendLine(" <PropertyGroup>");
            sb.AppendLine("     <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
            sb.AppendLine("     <EnablePackageVersionOverride>true</EnablePackageVersionOverride>");
            sb.AppendLine(" </PropertyGroup>");

            sb.AppendLine(" <ItemGroup>");
            foreach (var package in packages)
            {
                sb.AppendLine($"     <PackageVersion Include=\"{package.Key}\" Version=\"{getVersionFrom(package)}\" />");
            }
            sb.AppendLine(" </ItemGroup>");

            sb.AppendLine("</Project>");

            System.IO.File.WriteAllText(finfo.FullName, sb.ToString());
        }


        public static XmlPackagesVersionsProjectDOM Load(string path)
        {
            return Load<XmlPackagesVersionsProjectDOM>(path);
        }

        public string VerifyDocument(IReadOnlyList<XmlProjectDOM> csprojs)
        {
            try
            {

                var locals = GetPackageReferences().ToList();

                // check for duplicated entries

                var duplicated = locals
                    .GroupBy(item => item.PackageId)
                    .Where(item => item.Count() > 1)
                    .Select(item => item.Key)
                    .ToList();

                if (duplicated.Any())
                {
                    var msg = string.Join(" ", duplicated);
                    return $"Duplicated: {msg}";
                }

                // check cross references with projects:            

                foreach (var csproj in csprojs)
                {
                    var csprojRelPath = csproj.File.FullName.Substring(File.Directory.FullName.Length);

                    var csprojPackages = csproj.GetPackageReferences().ToList();
                    if (csprojPackages.Count == 0) continue;

                    // check if a csprojs PackageReference still have Version="xxx"

                    var withVersion = csprojPackages
                        .Where(item => item.Version != null)
                        .ToList();

                    if (withVersion.Count > 0)
                    {
                        var msg = string.Join("\r\n", withVersion.Select(item => item.PackageId));
                        return $"Version conflicts at {csprojRelPath}:\r\n {msg}";
                    }

                    // check if a PackageReference is not in PackageVersion

                    var missing = csprojPackages
                        .Where(item => !locals.Any(x => x.PackageId == item.PackageId))
                        .ToList();

                    if (missing.Count > 0)
                    {
                        var msg = string.Join("\r\n", missing.Select(item => item.PackageId));
                        return $"Version not set for {csprojRelPath}:\r\n {msg}";
                    }
                }

                return null;

            }
            catch (Exception ex) { return ex.Message; }
        }

        public override IEnumerable<XmlPackageReferenceVersion> GetPackageReferences(string itemName = "PackageReference")
        {
            return base.GetPackageReferences("PackageVersion");
        }
    }

}
