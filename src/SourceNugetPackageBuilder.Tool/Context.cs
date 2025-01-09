using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommandLine;


namespace SourceNugetPackageBuilder
{
    [System.Diagnostics.DebuggerDisplay("{SourceProjectPath.FullName,nq} => {OutputDirectory.FullName,nq}")]
    public class Arguments
    {
        #region arguments

        [Value(0, Required = true, HelpText = "Source files, which can be a solution or project files")]
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

        [Option("include-compile-checks", Required = false, HelpText = "includes a build.targets file that checks for common csproj mistakes")]
        public bool IncludeCompileChecks { get; set; }

        #endregion
    }

    [System.Diagnostics.DebuggerDisplay("{SourceProjectPath.FullName,nq} => {OutputDirectory.FullName,nq}")]
    public class Context : Arguments
    {
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
                
                factories = factories
                    .Where(item => item.IsPackableAsSources)
                    .ToList();
            }            

            foreach (var f in factories)
            {
                Console.Write($"Packing as sources: {f.ProjectPath.Name}...");

                var pbh = new PackageBuilHelper(f, this);
                pbh.AddIcon(f);
                pbh.AddFiles(f);
                if (IncludeCompileChecks) pbh.AddCompileChecks();

                pbh.SavePackage();

                Console.WriteLine("Completed");

                await Task.Yield();
            }
        }        

        #endregion
    }
}
