using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace SourceNugetPackageBuilder
{
    class PackageBuilHelper
    {
        #region lifecycle

        public PackageBuilHelper(ManifestFactory factory, Arguments args)
        {
            _Arguments = args;
            OutDir = args.OutputDirectory ?? factory.ProjectPath.Directory.DefineDirectory("bin");

            var config = _CreateManifestMetadata(factory, args);
            
            Builder = new PackageBuilder();
            Builder.Populate(config);

                      
        }

        private static ManifestMetadata _CreateManifestMetadata(ManifestFactory factory, Arguments args)
        {
            var config = factory.CreateMetadata();

            if (!string.IsNullOrWhiteSpace(args.AltPackageId)) { config.Id = args.AltPackageId; }
            if (args.AppendSourceSuffix && !config.Id.EndsWith(".Sources")) { config.Id += ".Sources"; }

            if (!config.Authors.Any()) config.Authors = new string[] { "unknown" };
            if (string.IsNullOrEmpty(config.Description)) config.Description = config.Id;

            if (!string.IsNullOrWhiteSpace(args.Version)) config.Version = NuGetVersion.Parse(args.Version);

            if (!string.IsNullOrWhiteSpace(args.VersionSuffix))
            {
                var dt = DateTime.Now;

                var suffix = args.VersionSuffix;
                suffix = suffix.Replace("{DATE}", dt.ToString("yyyyMMdd"));
                suffix = suffix.Replace("{SHORTDATE}", dt.ToString("yyMMdd"));
                suffix = suffix.Replace("{TIME}", dt.ToString("HHmmss"));
                suffix = suffix.Replace("{SHORTTIME}", dt.ToString("HHmm"));

                var v = config.Version;
                config.Version = new NuGetVersion(v.Major, v.Minor, v.Patch, suffix);
            }

            // must be a development dependency
            config.DevelopmentDependency = true;
            return config;
        }

        #endregion

        #region data

        private readonly Arguments _Arguments;
        public PackageBuilder Builder { get; }
        public System.IO.DirectoryInfo OutDir { get; set; }

        private int _TemplateFileCounter = 1;

        #endregion

        #region API

        public void AddIcon(ManifestFactory factory)
        {
            var icon = factory.FindIcon();
            if (icon == null || !icon.Exists) return;
            Builder.AddIcon(icon);
        }

        public void AddFiles(ManifestFactory factory)
        {
            foreach (var framework in factory.TargetFrameworks)
            {
                var fw = NuGetFramework.Parse(framework);

                // get files for this framework
                var files = factory
                    .FindCompilableFiles(framework)
                    .ToList();

                // validate

                foreach (var finfo in files)
                {
                    var ex = SourceCodeValidator.Validate(finfo.ReadAllText());
                    _Arguments.HandleSourceCodeValidationError(ex, finfo);
                }

                // add to package

                foreach (var finfo in files)
                {
                    _AddSourceCodeFile(factory, fw, finfo);
                }
            }            
        }

        public void AddCompileChecks()
        {
            var path = new System.IO.DirectoryInfo(AppContext.BaseDirectory).DefineFile("CompileChecks_targets.xml");
            if (!path.Exists) return;

            var f = CreatePhysicalPackageFile(path);
            f.TargetPath = $"build/CodeSugar.CompileChecks.targets";
            Builder.Files.Add(f);
        }

        private void _AddSourceCodeFile(ManifestFactory factory, NuGetFramework framework, FileInfo finfo)
        {
            var fileNameLC = finfo.Name.ToLowerInvariant();

            // special file names will not be included in the source package.
            if (fileNameLC == "_accessmodifiers.public.cs") return;
            if (fileNameLC == "_publicaccessmodifiers.cs") return;

            string targetPath = null;

            if (fileNameLC.EndsWith(".pp.cs") || fileNameLC.EndsWith(".cs.pp")) // template files are included directly
            {
                // template files need to use a path as short as
                // possible due to long paths generated on build
                // see; https://github.com/NuGet/Home/issues/13193
                targetPath = $"{_TemplateFileCounter}.cs.pp";
                _TemplateFileCounter++;
            }
            else if (fileNameLC.EndsWith(".cs"))
            {
                if (factory.ProjectPath.Directory.IsParentOf(finfo)) // source file inside project
                {
                    targetPath = finfo.GetPathRelativeTo(factory.ProjectPath.Directory);

                    if (!string.IsNullOrWhiteSpace(factory.PackAsSourcesFolder))
                    {
                        targetPath = System.IO.Path.Combine(factory.PackAsSourcesFolder, targetPath);
                    }

                    targetPath = targetPath.Replace("\\", "/");
                }
                else // file is outside the project's cone, maybe a link.
                {
                    var basePath = string.IsNullOrWhiteSpace(factory.PackAsSourcesFolder)
                        ? "Shared"
                        : factory.PackAsSourcesFolder;

                    targetPath = $"{basePath}/{finfo.Name}";
                }
            }

            // var body = finfo.ReadAllLines();
            // if (framework != "netstandard2.0" && body[0] =="#if NETSTANDARD2_0") continue;

            if (string.IsNullOrWhiteSpace(targetPath)) return;

            // add missing platform version to prevent NU1012
            // https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1012
            // https://learn.microsoft.com/en-us/dotnet/standard/frameworks#os-version-in-tfms            


            // add the files

            var frameworkName = framework.GetSanitizedFrameworkMoniker();

            var pkgFile = factory.PackAsInternalSources && MakeInternalRewriter.TryProcess(finfo.ReadAllText(), out var internalText)
                    ? CreatePhysicalPackageFromText(internalText)
                    : CreatePhysicalPackageFile(finfo);

            pkgFile.TargetPath = $"contentFiles/cs/{frameworkName}/{targetPath}";
            Builder.Files.Add(pkgFile);

            var manifest = new ManifestContentFiles();
            manifest.Include = pkgFile.TargetPath;
            manifest.BuildAction = "Compile";
            Builder.ContentFiles.Add(manifest);
        }

        private static PhysicalPackageFile CreatePhysicalPackageFile(FileInfo finfo)
        {
            var pkgFile = new PhysicalPackageFile();
            pkgFile.SourcePath = finfo.FullName;
            return pkgFile;
        }

        private static PhysicalPackageFile CreatePhysicalPackageFromText(string textBody)
        {
            var m = new System.IO.MemoryStream(); // memory leak!!

            using (var w = new System.IO.StreamWriter(m, leaveOpen: true))
            {
                w.Write(textBody);
            }

            m.Position = 0;

            return new PhysicalPackageFile(m);
        }

        public void SavePackage()
        {
            var writePath = OutDir.DefineFileInfo($"{Builder.Id}.{Builder.Version}.nupkg");
            writePath.Directory.Create();

            using (var w = writePath.OpenWrite())
            {
                try
                {
                    Builder.Save(w);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }
        }

        #endregion
    }
}
