using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using NUGETPACKIDENTITY = NuGet.Packaging.Core.PackageIdentity;
using NUGETPACKMETADATA = NuGet.Protocol.Core.Types.IPackageSearchMetadata;
using NUGETPACKDEPENDENCIES = NuGet.Protocol.Core.Types.FindPackageByIdDependencyInfo;


namespace DirectoryPackagesTools.Client
{
    /// <summary>
    /// Helper class used to classify packages into usage categories
    /// </summary>
    internal class PackageClassifier
    {
        public static bool HasHiddenPrereleases(NUGETPACKMETADATA meta)
        {
            if (meta == null) return false;

            var name = meta.Identity.Id;

            if (name.StartsWith("System.")) return true;
            if (name.StartsWith("Xamarin.")) return true;
            if (name.StartsWith("Microsoft.")) return true;
            if (name.StartsWith("Prism.")) return true;
            if (name.StartsWith("Avalonia.")) return true;
            if (name == "Google.Protobuf") return true;
            if (name == "SkiaSharp") return true;
            if (name == "ClosedXML") return true;
            if (name == "Grpc.Tools") return true;
            if (name == "log4net") return true;
            if (name == "MathNet.Numerics") return true;
            if (name == "CommunityToolkit.Mvvm") return true;
            if (IsUnitTestPackage(meta)) return true;
            return false;
        }

        public PackageClassifier(IEnumerable<string> prefixes)
        {
            // find package prefixes shared between at least 3 packages
            var commonPrefixes = prefixes
                .GroupBy(item => item).Where(item => item.Count() >= 3)
                .Select(item => item.Key)
                .ToArray();

            _CommonPrefixes = commonPrefixes;
        }

        private readonly string[] _CommonPrefixes;

        public string GetPackageCategory(NUGETPACKMETADATA metadata)
        {
            if (metadata == null) return "Null";

            if (metadata?.Identity?.Id == null) return "Null";

            if (IsUnitTestPackage(metadata)) return "Unit Tests";
            if (IsAvaloniaPackage(metadata)) return "Avalonia";
            if (IsAzurePackage(metadata)) return "Azure";
            if (IsSystemPackage(metadata)) return "System";            

            if (_CommonPrefixes != null)
            {
                var prefix = _CommonPrefixes.FirstOrDefault(item => metadata.Identity.Id.StartsWith(item));
                if (prefix != null) return prefix;
            }            

            return "Unsorted";
        }

        internal static bool IsUnitTestPackage(NUGETPACKMETADATA metadata)
        {
            if (metadata == null) return false;

            if (metadata?.Identity?.Id == null) return false;

            if (metadata.Tags != null)
            {
                var tags= metadata.Tags.Split(",").Select(item => item.Trim().ToLower()).ToList();

                if (tags.Contains("mstest") || tags.Contains("mstest2") || tags.Contains("nunit") || tags.Contains("xunit")) return true;
                if (tags.Contains("test") || tags.Contains("testing") || tags.Contains("unit-test") || tags.Contains("unittest")) return true;

                if (tags.Contains("analyzers")) return true;
            }

            return _IsTestPackage(metadata.Identity.Id);
        }

        private static bool IsAzurePackage(NUGETPACKMETADATA metadata)
        {
            if (metadata == null) return false;

            var id = metadata.Identity.Id;

            if (id.ToLower().Contains("azure")) return true;

            if (metadata.Tags != null)
            {
                var tags = metadata.Tags.Split(",").Select(item => item.Trim().ToLower()).ToList();

                if (tags.Contains("azure")) return true;
                if (tags.Contains("microsoftazure")) return true;
            }

            var authors = metadata?.Authors;

            if (authors != null)
            {                
                if (authors.Contains("azure-sdk")) return true;
            }

            return _IsTestPackage(metadata.Identity.Id);
        }

        private static bool IsAvaloniaPackage(NUGETPACKMETADATA metadata)
        {
            if (metadata == null) return false;

            var id = metadata.Identity.Id;

            if (id.ToLower().Contains("avalonia")) return true;

            return false;
        }

        private static bool IsSystemPackage(NUGETPACKMETADATA metadata)
        {
            if (metadata == null) return false;

            var id = metadata.Identity.Id;            

            if (SystemPackages.Contains(id)) return true;
            if (SystemPrefixes.Any(p => id.StartsWith(p + "."))) return true;

            var authors = metadata?.Authors;

            if (authors != null)
            {
                if (authors.Contains("Microsoft")) return true;
                if (authors.Contains("dotnetframework")) return true;
                if (authors.Contains("dotnetfoundation")) return true;
                if (authors.Contains("azure-sdk")) return true;
            }

            return false;
        }

        private static bool _IsTestPackage(string packageName)
        {
            if (TestPackages.Contains(packageName)) return true;

            packageName = packageName.ToLower();

            if (TestPrefixes.Any(p => packageName.StartsWith(p.ToLower() + "."))) return true;

            return false;
        }

        private static readonly IReadOnlyList<string> SystemPrefixes = new[] { "System", "Microsoft", "Azure", "Google", "Xamarin", "MathNet", "MonoGame", "Newtonsoft" };

        private static readonly IReadOnlyList<string> SystemPackages = new[] { "log4net", "DotNetZip", "ClosedXML", "Humanizer" };

        private static readonly IReadOnlyList<string> TestPrefixes = new[] { "NUnit", "coverlet", "TestAttachments", "TestImages", "ErrorProne" };

        private static readonly IReadOnlyList<string> TestPackages = new[] { "NUnit", "Microsoft.NET.Test.Sdk", "NUnit3TestAdapter" };
    }
}
