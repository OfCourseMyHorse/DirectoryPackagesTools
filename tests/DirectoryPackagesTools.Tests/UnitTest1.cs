using System;
using System.Linq;
using System.Threading;

using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using NUnit.Framework;

using DirectoryPackagesTools.DOM;

namespace DirectoryPackagesTools
{
    [AttachmentPathFormat("*/?")]
    [ResourcePathFormat("*")]
    public class UnitTest1
    {
        [SetUp]
        public void Setup()
        {
            var v1 = VersionRange.Parse("1.0.0");
            var v2 = VersionRange.Parse("1.0.0-preview3");
            var v3 = VersionRange.Parse("[1.0.0-preview3]");
        }

        
        [TestCase("versions.props")]
        [TestCase("versions2.props")]
        public void TestLoadVersions(string propsName)
        {
            var path = ResourceInfo.From(propsName);

            var props = XmlPackagesVersionsProjectDOM.Load(path);

            AttachmentInfo.From("xdp.xml").WriteObject(f => props.Save(f));

            var prefs = props.GetPackageReferences().ToList();

            Assert.That(prefs, Has.Count.EqualTo(5));

            foreach (var p in prefs)
            {
                Assert.That(p.PackageId, Is.Not.Null);
                Assert.That(p.Version, Is.Not.Null);

                TestContext.Out.WriteLine($"{p.PackageId} {p.Version}");
            }            
        }

        [TestCase("tests.props")]        
        public void TestLoadProject(string propsName)
        {
            var path = ResourceInfo.From(propsName);

            var props = XmlMSBuildProjectDOM.Load<XmlMSBuildProjectDOM>(path);

            AttachmentInfo.From("xdp.xml").WriteObject(f => props.Save(f));

            foreach (var p in props.GetPackageReferences())
            {
                TestContext.Out.WriteLine($"{p.PackageId} {p.Version}");
            }
        }

        [TestCase("dotnet-tools_x.json")]
        public void TestLoadTools(string propsName)
        {
            var path = ResourceInfo.From(propsName);

            var props = JsonToolsVersionsProjectDOM.Load(path);

            var views = props.GetPackageReferences().ToArray();

            TestContext.Out.WriteLine($"{views.Length}");
        }


        [Test]
        public async System.Threading.Tasks.Task ListPackages()
        {
            var nuClient = new Client.NuGetClient();

            using (var context = nuClient.CreateContext(CancellationToken.None))
            {
                foreach (var r in context.Repositories)
                {
                    // if (r.IsNugetOrg || r.IsOfficial || r.IsVisualStudio) continue;

                    TestContext.Out.WriteLine($"Repository {r.Source}");

                    var filter = new SearchFilter(true);

                    var result = await r.SearchAsync(filter, "", 0, 20);

                    foreach (var foundPackage in result)
                    {
                        TestContext.Out.WriteLine($"{foundPackage.Identity}");
                    }                    
                }
            }
        }

        [TestCase("DotNetZip")]
        [TestCase("System.Numerics.Vectors")]
        [TestCase("InteropTypes.Tensors.ONNX.Sources")]
        public async System.Threading.Tasks.Task GetPackageInfo(string packageName)
        {
            var nuClient = new Client.NuGetClient();

            using(var context = nuClient.CreateContext(CancellationToken.None))
            {
                foreach(var r in context.Repositories)
                {
                    var versions = await r.GetVersionsAsync(packageName);

                    if (versions == null || versions.Length == 0) continue;

                    var metas = await r.GetMetadataAsync(packageName);

                    TestContext.Out.WriteLine();
                    TestContext.Out.WriteLine($"--------------------------------------- From: " + r.Source.PackageSource);
                    TestContext.Out.WriteLine();

                    foreach (var v in versions)
                    {
                        var pid = new PackageIdentity(packageName, v);                        

                        var isLocal = await r.ExistLocally(pid);                        

                        TestContext.Out.WriteLine($"{v} Exists Locally:{isLocal}");
                        TestContext.Out.WriteLine();

                        var depInfo = await r.GetDependencyInfoAsync(pid);
                        foreach (var dg in depInfo.DependencyGroups)
                        {
                            TestContext.Out.WriteLine($"       {dg.TargetFramework}");

                            foreach(var jj in dg.Packages)
                            {
                                TestContext.Out.WriteLine($"           {jj}");
                            }
                        }
                    }                    

                    foreach(var meta in metas)
                    {
                        TestContext.Out.WriteLine(_ToJson(meta));

                        var deprecation = await meta.GetDeprecationMetadataAsync();

                        TestContext.Out.WriteLine(_ToJson(deprecation));
                    }                    
                }                
            }            
        }

        private static string _ToJson(Object obj)
        {
            try
            {
                if (obj == null) return "NULL";

                var opts = new System.Text.Json.JsonSerializerOptions();
                opts.IncludeFields = true;
                opts.WriteIndented = true;

                return System.Text.Json.JsonSerializer.Serialize(obj, opts);
            }
            catch (Exception ex) { return "ERROR"; }
        }

    }
}