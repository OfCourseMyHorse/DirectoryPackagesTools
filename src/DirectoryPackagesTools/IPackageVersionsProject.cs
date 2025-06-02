using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Versioning;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Represents a document containing package versions.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="DOM.XmlMSBuildProjectDOM"/>,
    /// <see cref="DOM.XmlPackagesVersionsProjectDOM"/> and
    /// <see cref="DOM.JsonToolsVersionsProjectDOM"/>
    /// </remarks>
    internal interface IPackageVersionsProject
    {
        public static readonly IPackageVersionsProject Empty = new _EmptyPackageVersionsProject();

        System.IO.FileInfo File { get; }
        IEnumerable<IPackageReferenceVersion> GetPackageReferences();
        void Save(string path = null);
    }

    sealed class _EmptyPackageVersionsProject : IPackageVersionsProject
    {
        public FileInfo File => null;

        public IEnumerable<IPackageReferenceVersion> GetPackageReferences()
        {
            yield break;
        }

        public void Save(string path = null)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Represents a package reference within a document.
    /// </summary>
    /// <remarks>
    /// Implemented by <see cref="DOM.XmlPackageReferenceVersion"/> and <see cref="DOM.JsonPackageReferenceVersion"/>
    /// </remarks>
    internal interface IPackageReferenceVersion
    {
        /// <summary>
        /// The name of the package: Ex: "System.Numerics.Vectors"
        /// </summary>
        string PackageId { get; }

        /// <summary>
        /// Package prefix: Ex: "System"
        /// </summary>
        string PackagePrefix => new string(PackageId.TakeWhile(c => c != '.').ToArray());

        /// <summary>
        /// Gets or sets SemVer version of the package
        /// </summary>
        VersionRange Version { get; set; }

        /// <summary>
        /// Removes the version attribute of the document.
        /// </summary>
        /// <remarks>
        /// When this is a CsProj PackageReference, it removes the 'Version' attribute.
        /// </remarks>
        void RemoveVersion();
        void SetVersion(IReadOnlyDictionary<string, string> packageVersions);
    }
}
