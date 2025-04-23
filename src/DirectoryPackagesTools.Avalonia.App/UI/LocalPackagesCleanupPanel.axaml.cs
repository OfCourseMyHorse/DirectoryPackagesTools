using System;
using System.Collections.Generic;
using System.IO;
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
        public static async Task ShowDialog(Window wnd)
        {
            var window = new Window();

            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            window.ShowInTaskbar = false;
            window.SizeToContent = SizeToContent.WidthAndHeight;

            window.Title = "Nuget package cache trimmer.";

            window.Content = new LocalPackagesCleanupPanel();

            await window.ShowDialog(wnd);
        }

        private LocalPackagesCleanupPanel()
        {
            InitializeComponent();
        }

        #endregion

        #region data

        private List<System.IO.DirectoryInfo> _DirectoriesToDelete;

        #endregion

        #region events

        private async void _OnClick_FindPackagesToDelete(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            this.IsEnabled = false;

            var rootDir = new System.IO.DirectoryInfo(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"));

            var toDelete = new List<System.IO.DirectoryInfo>();

            await _ProcessAsync(rootDir, toDelete, this).ConfigureAwait(true);            

            myPackagesToDelete.ItemsSource = toDelete.Select(item => new CachePackageInfo(item)).ToList();

            _DirectoriesToDelete = toDelete;

            this.IsEnabled = true;
        }

        private async void _OnClick_DeleteListedPackages(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_DirectoriesToDelete == null) return;

            myPackagesToDelete.ItemsSource = null;

            foreach(var d in _DirectoriesToDelete)
            {
                d.Delete(true);
            }

            _DirectoriesToDelete = null;
        }

        public static async Task _ProcessAsync(System.IO.DirectoryInfo packagesPath, List<System.IO.DirectoryInfo> toDelete, IProgress<int> progress)
        {
            if (!packagesPath.Exists) return;

            var packagesDirs = packagesPath.GetDirectories();

            for(int i=0; i < packagesDirs.Length; ++i)
            {
                var path = packagesDirs[i];

                progress?.Report(i*100 / packagesDirs.Length);

                await _ProcessPackageAsync(path, toDelete);
            }
        }

        private static async Task _ProcessPackageAsync(System.IO.DirectoryInfo packagePath, List<System.IO.DirectoryInfo> toDelete)
        {
            if (!packagePath.Exists) return;

            var versionPaths = packagePath.GetDirectories();

            var pairs = versionPaths
                .Select(dinfo => (dinfo, NuGetVersion.TryParse(dinfo.Name, out var version) ? version : null))
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

                foreach (var item in items) toDelete.Add(item.dinfo);

                take = 1;
            }
        }

        public void Report(int percent)
        {
            
        }


        private static long _GetUsedDiskSpace(System.IO.DirectoryInfo directoryInfo)
        {
            return directoryInfo.EnumerateFiles("*", System.IO.SearchOption.AllDirectories).Select(item => item.Length).Sum();
        }

        #endregion
    }

    class CachePackageInfo
    {
        private readonly System.IO.DirectoryInfo _Directory;

        public CachePackageInfo(DirectoryInfo directory)
        {
            _Directory = directory;
        }

        public string PackageName => _Directory.Parent.Name;

        public string PackageVersion => _Directory.Name;

        public long PackageDiskSize => _Directory.EnumerateFiles("*", System.IO.SearchOption.AllDirectories).Select(item => item.Length).Sum();
    }
}