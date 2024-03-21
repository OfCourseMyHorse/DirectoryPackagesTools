using System.Linq;
using System.Threading.Tasks;

using Microsoft.Build.Locator;

namespace SourceNugetPackageBuilder
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var instance = MSBuildLocator
                .QueryVisualStudioInstances()
                .OrderByDescending(instance => instance.Version)
                .First();

            MSBuildLocator.RegisterInstance(instance);

            await Context.RunCommandAsync(args).ConfigureAwait(false);
        }
    }
}
