using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceNugetPackageBuilder
{

    public class Arguments
    {
        #region command bindings

        // https://learn.microsoft.com/en-us/dotnet/standard/commandline/

        static Arguments()
        {
            _Command =
            [
                _SourceFiles,
                _OutputDirectory,
                _AltPackageId,
                _AppendSourceSuffix,
                _Version,
                _VersionSuffix,
                _IncludeCompileChecks,
                _DisableErrorValidation
            ];
        }

        private static readonly System.CommandLine.RootCommand _Command;
        private static readonly Argument<System.IO.FileInfo[]> _SourceFiles = new Argument<System.IO.FileInfo[]>("SourceFiles") { Description = "Source files, which can be a solution or project files", Arity = ArgumentArity.ZeroOrMore };
        private static readonly Option<System.IO.DirectoryInfo> _OutputDirectory = new Option<System.IO.DirectoryInfo>("--output", "-o") { Description = "output directory" };

        private static readonly Option<bool> _DisableErrorValidation = new Option<bool>("--disable-sources-validation") { Description = "Disables source code validation" };

        private static readonly Option<string> _AltPackageId = new Option<string>("--package-id") { Description = "Alternative package ID (default is project name)" };
        private static readonly Option<bool> _AppendSourceSuffix = new Option<bool>("--append-sources-suffix") { Description = "appends .Sources to package Id" };
        private static readonly Option<string> _Version = new Option<string>("--package-version", "-v") { Description = "package version" };
        private static readonly Option<string> _VersionSuffix = new Option<string>("--version-suffix", "-v") { Description = "package prerelease version suffix" };

        private static readonly Option<bool> _IncludeCompileChecks = new Option<bool>("--include-compile-checks") { Description = "includes a build.targets file that checks for common csproj mistakes" };

        private static ParseResult ParseCommand(string[] args)
        {
            var cfg = new ParserConfiguration();
            var result = _Command.Parse(args, cfg);
            return result;
        }

        public void SetArguments(params string[] args)
        {
            var result = ParseCommand(args);

            SourceFiles = result.GetValue(_SourceFiles).ToImmutableArray();

            DisableErrorValidation = result.GetValue(_DisableErrorValidation);

            OutputDirectory = result.GetValue(_OutputDirectory);

            Version = result.GetValue(_Version)?.TrimStart();
            VersionSuffix = result.GetValue(_VersionSuffix)?.TrimStart();
            AltPackageId = result.GetValue(_AltPackageId)?.TrimStart();
            AppendSourceSuffix = result.GetValue(_AppendSourceSuffix);
            IncludeCompileChecks = result.GetValue(_IncludeCompileChecks);
        }

        #endregion

        #region arguments

        // [Value(0, Required = true, HelpText = "Source files, which can be a solution or project files")]
        public ImmutableArray<System.IO.FileInfo> SourceFiles { get; set; }

        public bool DisableErrorValidation { get; set; }

        // [Option('o', "output", Required = false, HelpText = "output directory")]
        public System.IO.DirectoryInfo OutputDirectory { get; set; }

        // [Option('v', "package-version", Required = false, HelpText = "package version")]
        public string Version { get; set; }

        // [Option("version-suffix", Required = false, HelpText = "package version suffix")]
        public string VersionSuffix { get; set; }

        // [Option("package-id", Required = false, HelpText = "Alternative package ID")]
        public string AltPackageId { get; set; }

        // [Option("append-sources-suffix", Required = false, HelpText = "appends .Sources to package Id")]
        public bool AppendSourceSuffix { get; set; }

        // [Option("include-compile-checks", Required = false, HelpText = "includes a build.targets file that checks for common csproj mistakes")]
        public bool IncludeCompileChecks { get; set; }

        #endregion

        #region API

        public void HandleSourceCodeValidationError(Exception ex, FileInfo sourceCodePath)
        {
            if (ex == null) return;

            if (DisableErrorValidation)
            {
                Console.Error.WriteLine($"{sourceCodePath.FullName} : {ex.Message}");
                return;
            }                
                
            throw new ArgumentException(sourceCodePath.FullName, ex);            
        }

        #endregion
    }

    public class Context : Arguments
    {
        #region API

        public static async Task RunCommandAsync(params string[] args)
        {
            var ctx = new Context();
            ctx.SetArguments(args);

            await ctx.RunAsync();
        }

        public async Task RunAsync()
        {
            if (SourceFiles == null || !SourceFiles.Any())
            {
                var currDir = new System.IO.DirectoryInfo(Environment.CurrentDirectory);
                var defaultFile = currDir.EnumerateFiles("*.sln").FirstOrDefault();
                defaultFile ??= currDir.EnumerateFiles("*.csproj").FirstOrDefault();
                SourceFiles = new[] { defaultFile }.ToImmutableArray();
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
