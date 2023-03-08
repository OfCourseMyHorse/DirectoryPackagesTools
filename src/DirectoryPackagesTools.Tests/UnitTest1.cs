using System;
using System.Collections.Generic;
using System.Threading;
using DirectoryPackagesTools.DOM;

using NuGet.Packaging.Core;

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

                        TestContext.WriteLine($"{v} {isLocal}");
                    }
                }                
            }            
        }

    }
}