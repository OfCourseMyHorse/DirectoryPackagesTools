using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DirectoryPackagesTools.DOM
{
    /// <summary>
    ///  MsBuild csproj utils
    /// </summary>
    internal static class _ProjectUtils
    {
        public static XElement GetRoot(this XElement element)
        {
            if (element == null) return null;
            if (element.Parent == null) return element;
            return element.GetRoot();
        }

        public static IEnumerable<System.IO.FileInfo> EnumerateProjects(System.IO.DirectoryInfo dinfo)
        {
            if (dinfo.LinkTarget != null)
            {
                System.Console.WriteLine($"Found Link at: {dinfo.FullName} pointing at  {dinfo.LinkTarget}");
                return Enumerable.Empty<System.IO.FileInfo>();
            }

            var files = dinfo
                .EnumerateFiles()
                .Where(_IsValidFile);

            var dfiles = dinfo
                .EnumerateDirectories()
                .Where(_IsValidDir)
                .SelectMany(item => EnumerateProjects(item));

            return files.Concat(dfiles);
        }

        private static bool _IsValidFile(System.IO.FileInfo file)
        {
            if (file.Name.ToLower() == "directory.packages.props") return false;

            var ext = file.Extension.ToLower();

            if (ext.EndsWith(".csproj")) return true;
            if (ext.EndsWith(".targets")) return true;
            if (ext.EndsWith(".props")) return true;
            return false;
        }

        private static bool _IsValidDir(System.IO.DirectoryInfo dinfo)
        {
            if (dinfo.LinkTarget != null)
            {
                System.Console.WriteLine($"Found Link at: {dinfo.FullName} pointing at  {dinfo.LinkTarget}");
                return false;
            }

            if (string.Equals(dinfo.Name, "bin", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(dinfo.Name, "obj", StringComparison.OrdinalIgnoreCase)) return false;

            if (dinfo.EnumerateFiles().Any(item => item.Name.ToLower() == "directory.packages.props")) return false;

            return true;
        }
    }
}
