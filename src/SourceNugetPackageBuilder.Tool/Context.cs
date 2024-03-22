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
        #region output

        [Option('p', "project", Required = true, HelpText = "path to source project file")]
        public System.IO.FileInfo SourceProjectPath { get; set; }

        [Option('o', "outdir", Required = false, HelpText = "output directory")]
        public System.IO.DirectoryInfo OutputDirectory { get; set; }

        [Option('v', "package-version", Required = false, HelpText = "package version")]
        public string Version { get; set; }

        [Option("version-suffix", Required = false, HelpText = "package version suffix")]
        public string VersionSuffix { get; set; }

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
            OutputDirectory ??= SourceProjectPath.Directory.DefineDirectory("bin");

            var factory = ManifestFactory.Create(SourceProjectPath);

            await Task.Yield();

            var config = factory.CreateMetadata();

            if (AppendSourceSuffix) { config.Id += ".Sources"; }

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
            var writePath = OutputDirectory.DefineFile($"{builder.Id}.{builder.Version}.nupkg");
            writePath.Directory.Create();            

            using(var w = writePath.OpenWrite())
            {
                try
                {
                    builder.Save(w);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }                
            }

            await Task.Yield();
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

            var pkgFile = new PhysicalPackageFile();
            pkgFile.SourcePath = finfo.FullName;
            pkgFile.TargetPath = $"contentFiles/cs/{framework}/{targetPath}";
            builder.Files.Add(pkgFile);

            var manifest = new ManifestContentFiles();
            manifest.Include = pkgFile.TargetPath;
            manifest.BuildAction = "Compile";
            builder.ContentFiles.Add(manifest);            
        }

        private static PhysicalPackageFile CreatePhysicalPackageFileFromText(string textBody)
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
