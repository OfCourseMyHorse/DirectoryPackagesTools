
### Understanding NuGet APIs

It all boils down to this:

The initialization happens by querying the gallery sources, this scans the target directory for NuGet.Config files to create a collection of Gallery Sources.

The main problem is that most of the API to  interact with packages is per repository.

     RepositoryA → package
     RepositoryB → package

What we need is an API that inverts that dependency

    Package → RepositoryA
    Package → RepositoryB

Furthermore, altough it's rare, a Package can have multiple repository sources, for example official releases from NuGet and Nightly Builds from Github

    Package → RepositoryA
            ⮡ RepositoryB

So we need an API that exposes a "Package" and internally contains all its found "sources"