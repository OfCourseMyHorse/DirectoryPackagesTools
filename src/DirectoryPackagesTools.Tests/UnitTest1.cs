using System;
using System.Collections.Generic;
using System.Threading;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

using NUnit.Framework;
using NUnit.Framework.Internal;

namespace DirectoryPackagesTools
{
    [AttachmentPathFormat("*/?")]
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            var props = PropsDOM.Load("dp.props");

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

        [Test]
        public async System.Threading.Tasks.Task Test3()
        {
            var mvvm = await PropsMVVM.Load("", null);

            
        }



    }
}