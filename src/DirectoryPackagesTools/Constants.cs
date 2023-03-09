using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DirectoryPackagesTools
{
    internal static class Constants
    {
        public static bool IsTestPackage(string packageName)
        {
            if (TestPackages.Contains(packageName)) return true;

            packageName = packageName.ToLower();

            if (TestPrefixes.Any(p => packageName.StartsWith(p.ToLower() + "."))) return true;

            return false;
        }

        public static readonly IReadOnlyList<string> SystemPrefixes = new[] { "System","Microsoft","Azure","Google", "Xamarin", "Prism", "MathNet", "MonoGame"};

        public static readonly IReadOnlyList<string> SystemPackages = new[] { "log4net", "DotNetZip", "ClosedXML", "Humanizer" };


        public static readonly IReadOnlyList<string> TestPrefixes = new[] { "NUnit", "coverlet", "TestAttachments", "TestImages", "ErrorProne" };

        public static readonly IReadOnlyList<string> TestPackages = new[] { "NUnit", "Microsoft.NET.Test.Sdk", "NUnit3TestAdapter" };

    }
}
