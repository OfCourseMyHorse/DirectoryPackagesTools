## SourceNugetPackageBuilder.Tool

### Overview

Packaging source code directly into a nuget package is a rarely used, yet useful feature in some scenarios,
like glue code, or small code snippets that are not large enough to deserve their own package.

Also source code packages, when consumed with 'PrivateAssets="all"' are not transitive, so there's no risk of version collision.

Unfortunately, configuring a csproj file to generate a source code only package has a number of issues:
- It is so obscure that could be considered a "dark art"
- templated files (.cs.pp) are hard to work with
- the project itself cannot be referenced by other projects within the solution and cannot be easily tested.


SourceNugetPackageBuilder addresses these issues by packing a regular project as a source code nuget package.

### Requirements for consumer projects

- The project **must** have a `<RootNamespace>namespace</RootNamespace>` property defined.
- At the very least, the package reference needs to define the property `PrivateAssets="all"`
- If the sources package requires some dependencies, like System.Numerics.Vectors, the project needs to reference them


### Source code Nuget package examples:

- [Microsoft.Azure.WebJobs.Sources](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Sources)
- [Seterlund.CodeGuard.Source](https://www.nuget.org/packages/Seterlund.CodeGuard.Source)

### Nuget source package references

- [Content v2 for project.json](https://github.com/NuGet/Home/wiki/%5BSpec%5D-Content-v2-for-project.json)
- [Nuget Issues](https://github.com/NuGet/Home/issues?q=is%3Aissue+is%3Aopen+.cs.pp)