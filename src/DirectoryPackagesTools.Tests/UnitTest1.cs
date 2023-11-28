using System;
using System.Collections.Generic;
using System.Threading;
using DirectoryPackagesTools.DOM;

using NuGet.Packaging.Core;
using NuGet.Versioning;

using NUnit.Framework;
using NUnit.Framework.Internal;

namespace DirectoryPackagesTools
{
    [AttachmentPathFormat("*/?")]
    [ResourcePathFormat("*")]
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            var v1 = VersionRange.Parse("1.0.0");
            var v2 = VersionRange.Parse("1.0.0-preview3");
            var v3 = VersionRange.Parse("[1.0.0-preview3]");


        }

        [TestCase("tests.props")]
        [TestCase("versions.props")]
        public void TestLoadVersions(string propsName)
        {
            var path = ResourceInfo.From(propsName);

            var props = XmlPackagesVersionsProjectDOM.Load(path);

            AttachmentInfo.From("xdp.xml").WriteObject(f => props.Save(f));


            foreach(var p in props.GetPackageReferences())
            {
                TestContext.WriteLine($"{p.PackageId} {p.Version}");
            }
        }

        [TestCase("tests.props")]        
        public void TestLoadProject(string propsName)
        {
            var path = ResourceInfo.From(propsName);

            var props = XmlProjectDOM.Load<XmlProjectDOM>(path);

            AttachmentInfo.From("xdp.xml").WriteObject(f => props.Save(f));

            foreach (var p in props.GetPackageReferences())
            {
                TestContext.WriteLine($"{p.PackageId} {p.Version}");
            }
        }

        [Test]
        public async System.Threading.Tasks.Task Test2()
        {
            var name = "System.Numerics.Vectors";

            var nuClient = new Client.NuGetClient();

            using(var context = nuClient.CreateContext(CancellationToken.None))
            {
                foreach(var r in context.Repositories)
                {
                    TestContext.WriteLine();
                    TestContext.WriteLine(r.Source.PackageSource);

                    var versions = await r.GetVersionsAsync("System.Numerics.Vectors");

                    foreach (var v in versions)
                    {
                        var pid = new PackageIdentity(name, v);

                        var isLocal = await r.ExistLocally(pid);
                        var depInfo = await r.GetDependencyInfoAsync(pid);

                        TestContext.WriteLine($"{v} {isLocal}");

                        foreach(var kk in depInfo.DependencyGroups)
                        {
                            TestContext.WriteLine($"       {kk.TargetFramework}");

                            foreach(var jj in kk.Packages)
                            {
                                TestContext.WriteLine($"           {jj}");
                            }
                        }
                    }

                    var metas = await r.GetMetadataAsync("System.Numerics.Vectors");

                    foreach(var meta in metas)
                    {
                        TestContext.WriteLine(meta.Owners);
                    }

                    
                }                
            }            
        }

    }
}