using System;
using System.Collections.Generic;
using System.Threading;

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
            var nuClient = new NuGetClient();

            var versions = await nuClient.GetVersions("System.Numerics.Vectors");

            foreach(var v in versions) TestContext.WriteLine(v.ToString());
        }

    }
}