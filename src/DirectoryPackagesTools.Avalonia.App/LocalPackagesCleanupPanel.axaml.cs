using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using NuGet.Versioning;

namespace DirectoryPackagesTools
{

    partial class LocalPackagesCleanupPanel : UserControl, IProgress<int>
    {
        #region lifecycle

        #region lifecycle
        public static async Task ShowDialog(Window wnd)
        {
            var window = new Window();

            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            window.ShowInTaskbar = false;
            window.SizeToContent = SizeToContent.WidthAndHeight;

            window.Title = "Nuget local packages cleanup";

            window.Content = new LocalPackagesCleanupPanel();

            await window.ShowDialog(wnd);
        }

        public LocalPackagesCleanupPanel()
        {
            InitializeComponent();
        }

        #endregion

        private List<string> _DirectoriesToDelete;

        private async void _OnClick_FindPackagesToDelete(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.IsEnabled = false;

            var rootDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

            var toDelete = new List<string>();

            await _ProcessAsync(rootDir, toDelete, this).ConfigureAwait(true);

            string _toDisplatName(string fullPath)
            {
                var semver = System.IO.Path.GetFileName(fullPath);
                fullPath = System.IO.Path.GetDirectoryName(fullPath);
                var pkgName = System.IO.Path.GetFileName(fullPath);
                return pkgName + "." + semver;
            }

            myPackagesToDelete.ItemsSource = toDelete.Select(_toDisplatName).ToList();

            _DirectoriesToDelete = toDelete;

            this.IsEnabled = true;
        }

        private async void _OnClick_DeleteListedPackages(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_DirectoriesToDelete == null) return;

            myPackagesToDelete.ItemsSource = null;

            foreach(var d in _DirectoriesToDelete)
            {
                System.IO.Directory.Delete(d, true);
            }

            _DirectoriesToDelete = null;
        }

        public static async Task _ProcessAsync(string packagesPath, List<string> toDelete, IProgress<int> progress)
        {
            if (!System.IO.Directory.Exists(packagesPath)) return;

            var packagesDirs = System.IO.Directory.GetDirectories(packagesPath);

            for(int i=0; i < packagesDirs.Length; ++i)
            {
                var path = packagesDirs[i];

                progress?.Report(i*100 / packagesDirs.Length);

                await _ProcessPackageAsync(path, toDelete);
            }
        }

        private static async Task _ProcessPackageAsync(string packagePath, List<string> toDelete)
        {
            if (!System.IO.Directory.Exists(packagePath)) return;

            var versionPaths = System.IO.Directory.GetDirectories(packagePath);

            var pairs = versionPaths
                .Select(path => (path, NuGetVersion.TryParse(System.IO.Path.GetFileName(path), out var version) ? version : null))
                .Where(item => item.Item2 != null)
                .ToList();

            var majors = pairs.GroupBy(item => item.Item2.Major);

            // heuristic:
            // - leaves the 3 latest versions of the major version
            // - leaves the latest version of every previous major version.

            int take = 3;

            foreach(var major in majors.OrderByDescending(item => item.Key))
            {
                var items = major.OrderByDescending(item => item.Item2)
                    .Skip(take)
                    .ToList();

                foreach (var item in items) toDelete.Add(item.path);

                take = 1;
            }
        }

        public void Report(int percent)
        {
            myProgressBar.Value = percent;
        }

        #endregion
    }
}