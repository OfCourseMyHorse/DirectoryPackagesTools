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



### Source code Nuget package examples:

- [Microsoft.Azure.WebJobs.Sources](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Sources)
- [Seterlund.CodeGuard.Source](https://www.nuget.org/packages/Seterlund.CodeGuard.Source)