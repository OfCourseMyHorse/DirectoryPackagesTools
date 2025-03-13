
using NuGetPe.AssemblyMetadata;

using NUnit.Framework;

using System;
using System.Linq;

namespace Forensics
{

    public class AssemblyMetadataTests
    {
        [Explicit]
        [Test]
        public void ScanNuget()
        {
            var path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = System.IO.Path.Combine(path, ".nuget", "packages");

            var allAssemblies = System.IO.Directory.GetFiles(path, "*.dll", System.IO.SearchOption.AllDirectories);

            foreach (var assemblyPath in allAssemblies)
            {
                var finfo = new System.IO.FileInfo(assemblyPath);
                var framework = finfo.Directory.Name;                

                try
                {

                    var info = AssemblyMetadataReader.ReadMetaData(assemblyPath);

                    // https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/trimming-unsupported-targetframework
                    var netStdWithTrimmable = framework.Contains("netstandard") && info.MetadataEntries.Any(item => item.Value.Contains("IsTrimmable"));

                    if (!netStdWithTrimmable) continue;

                    TestContext.Out.WriteLine($"{assemblyPath}  {framework}");

                    /*
                    foreach (var entry in info.MetadataEntries)
                    {
                        TestContext.Out.WriteLine($"    {entry.Key} = {entry.Value}");
                    }

                    foreach (var aref in info.ReferencedAssemblies)
                    {
                        TestContext.Out.WriteLine($"      {aref.Name}");
                    }

                    if (framework.Contains("netstandard") && info.MetadataEntries.Any(item => item.Value.Contains("IsTrimmable")))
                    {
                        TestContext.Out.WriteLine("  WARNING: IsTrimmable is defined");
                    }*/
                }
                catch (Exception ex)
                {
                    // TestContext.Out.WriteLine($"  ERROR; {ex.Message}");
                }

            }
        }
    }
}
