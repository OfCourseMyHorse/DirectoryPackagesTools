using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NuGet.Packaging;
using NuGet.Versioning;
using CommandLine;
using System.IO;


namespace SourceNugetPackageBuilder
{
    [System.Diagnostics.DebuggerDisplay("{SourceProjectPath.FullName,nq} => {OutputDirectory.FullName,nq}")]
    public class Context
    {
        #region arguments

        [Value(0, Required =true, HelpText ="Source files, which can be a solution or project files")]
        public IEnumerable<System.IO.FileInfo> SourceFiles { get; set; }        

        [Option('o', "output", Required = false, HelpText = "output directory")]
        public System.IO.DirectoryInfo OutputDirectory { get; set; }

        [Option('v', "package-version", Required = false, HelpText = "package version")]
        public string Version { get; set; }

        [Option("version-suffix", Required = false, HelpText = "package version suffix")]
        public string VersionSuffix { get; set; }

        [Option("package-id", Required = false, HelpText = "Alternative package ID")]
        public string AltPackageId { get; set; }

        [Option("append-sources-suffix", Required = false, HelpText = "appends .Sources to package Id")]
        public bool AppendSourceSuffix { get; set; }        

        #endregion

        #region API

        public static async Task RunCommandAsync(params string[] args)
        {
            await Parser
                .Default
                .ParseArguments<Context>(args)
                .WithParsedAsync(async o => await o.RunAsync().ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        public async Task RunAsync()
        {
            if (SourceFiles == null || !SourceFiles.Any())
            {
                var currDir = new System.IO.DirectoryInfo(Environment.CurrentDirectory);
                var defaultFile = currDir.EnumerateFiles("*.sln").FirstOrDefault();
                defaultFile ??= currDir.EnumerateFiles("*.csproj").FirstOrDefault();
                SourceFiles = new[] { defaultFile };
            }            

            var factories = ManifestFactory.Create(SourceFiles).ToList();
            if (factories.Count > 1)
            {
                if (!string.IsNullOrWhiteSpace(AltPackageId)) throw new ArgumentException("--package-id can only be set for a single project");
                
                factories = factories.Where(item => item.IsPackableAsSources).ToList();
            }            

            foreach (var f in factories)
            {
                _PackProject(f);
                await Task.Yield();
            }
        }

        private void _PackProject(ManifestFactory factory)
        {
            Console.Write($"Packing as sources: {factory.ProjectPath.Name}...");

            var config = factory.CreateMetadata();

            if (!string.IsNullOrWhiteSpace(AltPackageId)) { config.Id = AltPackageId; }
            if (AppendSourceSuffix && !config.Id.EndsWith(".Sources")) { config.Id += ".Sources"; }

            if (!config.Authors.Any()) config.Authors = new string[] { "unknown" };
            if (string.IsNullOrEmpty(config.Description)) config.Description = config.Id;

            if (!string.IsNullOrWhiteSpace(Version)) config.Version = NuGetVersion.Parse(this.Version);

            if (!string.IsNullOrWhiteSpace(VersionSuffix))
            {
                var dt = DateTime.Now;

                var suffix = VersionSuffix;
                suffix = suffix.Replace("{DATE}", dt.ToString("yyyyMMdd"));
                suffix = suffix.Replace("{SHORTDATE}", dt.ToString("yyMMdd"));
                suffix = suffix.Replace("{TIME}", dt.ToString("HHmmss"));
                suffix = suffix.Replace("{SHORTTIME}", dt.ToString("HHmm"));

                var v = config.Version;
                config.Version = new NuGetVersion(v.Major, v.Minor, v.Patch, suffix);
            }


            // must be a development dependency
            config.DevelopmentDependency = true;

            var builder = _BuildPackage(factory, config);
            // builder.ReleaseNotes =

            // save result
            var outDir = OutputDirectory ?? factory.ProjectPath.Directory.DefineDirectory("bin");
            var writePath = outDir.DefineFile($"{builder.Id}.{builder.Version}.nupkg");
            writePath.Directory.Create();

            using (var w = writePath.OpenWrite())
            {
                try
                {
                    builder.Save(w);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }

            Console.WriteLine("Completed");
        }

        private static PackageBuilder _BuildPackage(ManifestFactory factory, ManifestMetadata config)
        {
            // Create a builder for the package
            var builder = new PackageBuilder();

            builder.Populate(config);

            builder.AddIcon(factory.FindIcon());

            int templateFileCounter = 1;            

            foreach(var framework in factory.TargetFrameworks)
            {
                var files = factory.FindCompilableFiles(framework).ToList();

                foreach (var finfo in files)
                {
                    _AddSourceFile(factory, builder, ref templateFileCounter, framework, finfo);
                }
            }            

            return builder;
        }

        private static void _AddSourceFile(ManifestFactory factory, PackageBuilder builder, ref int templateFileCounter, string framework, FileInfo finfo)
        {
            var fileNameLC = finfo.Name.ToLowerInvariant();

            // special file names will not be included in the source package.
            if (fileNameLC == "_accessmodifiers.public.cs") return;
            if (fileNameLC == "_publicaccessmodifiers.cs") return;

            string targetPath = null;

            if (fileNameLC.EndsWith(".pp.cs") || fileNameLC.EndsWith(".cs.pp"))
            {
                // template files need to use a path as short as
                // possible due to long paths generated on build
                // see; https://github.com/NuGet/Home/issues/13193
                targetPath = $"{templateFileCounter}.cs.pp";
                templateFileCounter++;
            }
            else if (fileNameLC.EndsWith(".cs"))
            {
                if (factory.ProjectPath.Directory.IsParentOf(finfo))
                {
                    targetPath = finfo.GetPathRelativeTo(factory.ProjectPath.Directory).Replace("\\", "/");
                }
                else
                {
                    targetPath = $"Shared/{finfo.Name}"; // possibly a link
                }
            }

            // var body = finfo.ReadAllLines();
            // if (framework != "netstandard2.0" && body[0] =="#if NETSTANDARD2_0") continue;

            if (targetPath == null) return;

            var pkgFile = factory.PackAsInternalSources && MakeInternalRewriter.TryProcess(finfo.ReadAllText(), out var internalText)
                ? CreatePhysicalPackageFromText(internalText)
                : CreatePhysicalPackageFile(finfo);

            pkgFile.TargetPath = $"contentFiles/cs/{framework}/{targetPath}";
            builder.Files.Add(pkgFile);

            var manifest = new ManifestContentFiles();
            manifest.Include = pkgFile.TargetPath;
            manifest.BuildAction = "Compile";
            builder.ContentFiles.Add(manifest);            
        }

        private static PhysicalPackageFile CreatePhysicalPackageFile(System.IO.FileInfo finfo)
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

        #endregion
    }
}
