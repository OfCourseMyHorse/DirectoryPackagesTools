using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Locator;

using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Versioning;

using NUnit.Framework;

namespace DirectoryPackagesTools
{
    
    internal class TestCreatePackage
    {
        private static string _MSBuildPath;

        [SetUp]
        public void Setup()
        {
            if (!MSBuildLocator.IsRegistered && MSBuildLocator.CanRegister)
            {
                var instance = MSBuildLocator.QueryVisualStudioInstances().OrderByDescending(instance => instance.Version).First();
                MSBuildLocator.RegisterInstance(instance);

                _MSBuildPath = instance.MSBuildPath;
            }            
        }



        [Test]
        public void CreatePackageProgramatically()
        {
            // Define the path for the new package
            var packagePath = "YourPackage.nupkg";

            // Create a builder for the package
            var builder = new PackageBuilder();

            // Set the properties of the package
            builder.Id = "YourPackageId";
            builder.Version = new NuGetVersion("1.0.0");
            builder.Description = "Your package description";
            builder.Authors.Add("Your Name");

            var pkgFile = CreatePhysicalPackageFileFromText("hello world!");
            pkgFile.TargetPath = "lib/netstandard2.0/readme.txt";

            builder.Files.Add(pkgFile);

            AttachmentInfo.From(packagePath).WriteToStream(builder.Save);
        }

        public static PhysicalPackageFile CreatePhysicalPackageFileFromText(string textBody)
        {
            var m = new System.IO.MemoryStream(); // memory leak!!
            using (var w = new System.IO.StreamWriter(m, leaveOpen: true)) { w.Write(textBody); }
            m.Position = 0;
            return new PhysicalPackageFile(m);
        }

        [Test]
        public async Task CreateSourcePackageFromProject()
        {
            var prjPath = ResourceInfo.From("SourcePackageExampleProject/SourcePackageExampleProject.csproj");

            using (var testDir = new AttachmentDirectory())
            {

                var ctx = new SourceNugetPackageBuilder.Context();
                ctx.SourceFiles = [prjPath.File];
                ctx.VersionSuffix = "explicit-{SHORTDATE}-{SHORTTIME}";
                ctx.IncludeCompileChecks = true;

                ctx.OutputDirectory = testDir.Directory;

                await ctx.RunAsync().ConfigureAwait(false);
            }            
        }
    }
}
