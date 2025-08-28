using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

namespace SourceNugetPackageBuilder.Tests
{
    internal class CommandLineTests
    {
        [Test]
        public void SimpleArgs()
        {
            var args = new Arguments();
            args.SetArguments("test.csproj", "-v", "1.2.3");
            Assert.That(args.SourceFiles, Has.Length.EqualTo(1));
            Assert.That(args.Version, Is.EqualTo("1.2.3"));

            args.SetArguments("test.csproj", "--package-version", "1.2.3");
            Assert.That(args.SourceFiles, Has.Length.EqualTo(1));
            Assert.That(args.Version, Is.EqualTo("1.2.3"));

            args.SetArguments("test.csproj", "kk.csproj", "-v", "1.2.3", "--version-suffix", "-pre", "--include-compile-checks");
            Assert.That(args.SourceFiles, Has.Length.EqualTo(2));
            Assert.That(args.Version, Is.EqualTo("1.2.3"));
            Assert.That(args.VersionSuffix, Is.EqualTo("-pre"));
            Assert.That(args.IncludeCompileChecks);




        }
    }
}
