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

        [Test]
        public void Test1()
        {
            var path = ResourceInfo.From("versions.props");

            var props = XmlPackagesVersionsProjectDOM.Load(path);

            AttachmentInfo.From("xdp.xml").WriteObject(f => props.Save(f));


            foreach(var p in props.GetPackageReferences())
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