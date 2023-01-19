using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Microsoft.Win32;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IProgress<int>, IProgress<Exception>
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += _OnLoaded;
        }

        private void _OnLoaded(object sender, RoutedEventArgs e)
        {
            var path = Environment
                .GetCommandLineArgs()
                .Where(item => item.EndsWith("Directory.Packages.Props", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(path)) _LoadDocument(path);            
        }

        public void Report(int value)
        {
            this.Dispatcher.Invoke( ()=> myProgressBar.Value= value);
        }

        public void Report(Exception value)
        {
            this.Dispatcher.Invoke(() => MessageBox.Show(value.Message,"Error"));
        }

        private void MenuItem_Load(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.RestoreDirectory = true;
            dlg.Filter = "Package Versions|directory.packages.props";

            if (!dlg.ShowDialog().Value) return;
            _LoadDocument(dlg.FileName);
        }

        private void _LoadDocument(string documentPath)
        {
            myProgressBar.Visibility = Visibility.Visible;

            void _loadDocument()
            {
                var props = PackagesVersionsProjectMVVM
                    .Load(documentPath, this)
                    .ConfigureAwait(true)
                    .GetAwaiter()
                    .GetResult();

                if (props == null) return;

                this.Dispatcher.Invoke(() => { this.DataContext = props; myProgressBar.Visibility = Visibility.Collapsed; });
            }

            Task.Run(_loadDocument);
        }

        private void MenuItem_Save(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is PackagesVersionsProjectMVVM mvvm) mvvm.Save();
        }

        private void MenuItem_SaveAndCommit(object sender, RoutedEventArgs e)
        {
            MenuItem_Save(sender, e);

            
            if (this.DataContext is PackagesVersionsProjectMVVM mvvm)
            {
                var finfo = new System.IO.FileInfo(mvvm.DocumentPath);

                // TODO: check whether .git or .svn are in the directory, and launch appropiate frontend

                _CommitSVN(finfo.Directory);

                Application.Current.Shutdown();
            }
        }

        private static void _CommitSVN(DirectoryInfo dinfo)
        {
            // https://tortoisesvn.net/docs/release/TortoiseSVN_en/tsvn-automation.html

            var exePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            exePath = System.IO.Path.Combine(exePath, "TortoiseSVN\\bin\\TortoiseProc.exe");

            var psi = new System.Diagnostics.ProcessStartInfo(exePath, $"/command:commit /path:\"{dinfo.FullName}\" /logmsg:\"nugets++\"");
            psi.UseShellExecute = true;
            psi.WorkingDirectory = dinfo.FullName;

            System.Diagnostics.Process.Start(psi);
        }

        private void MenuItem_OpenCommandLine(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is PackagesVersionsProjectMVVM mvvm)
            {
                var finfo = new System.IO.FileInfo(mvvm.DocumentPath);                

                _OpenCommandLine(finfo.Directory);
            }
        }

        private static void _OpenCommandLine(DirectoryInfo dinfo)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = true;
            psi.WorkingDirectory = dinfo.FullName;

            System.Diagnostics.Process.Start(psi);
        }

        private void MenuItem_New(object sender, RoutedEventArgs e)
        {
            var dir = _SelectDirectoryDialog();
            if (dir == null) return;

            var finfo = new System.IO.FileInfo(System.IO.Path.Combine(dir, "Directory.Packages.props"));
            if (finfo.Exists)
            {
                if (MessageBox.Show("Overwrite?", "File already exists", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            }

            var packages = XmlProjectDOM
                .FromDirectory(finfo.Directory)
                .SelectMany(item => item.GetPackageReferences())
                .GroupBy(item => item.PackageId)
                .OrderBy(item => item.Key);

            string getVersionFrom(IEnumerable<XmlPackageReferenceVersion> references)
            {
                references = references.Where(item => item.Version != null);
                if (!references.Any()) return "0";

                return references.First().Version;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Project>");

            sb.AppendLine(" <PropertyGroup>");
            sb.AppendLine("     <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>");
            sb.AppendLine("     <EnablePackageVersionOverride>true</EnablePackageVersionOverride>");
            sb.AppendLine(" </PropertyGroup>");

            sb.AppendLine(" <ItemGroup>");
            foreach(var package in packages)
            {
                sb.AppendLine($"     <PackageVersion Include=\"{package.Key}\" Version=\"{getVersionFrom(package)}\" />");
            }
            sb.AppendLine(" </ItemGroup>");

            sb.AppendLine("</Project>");

            System.IO.File.WriteAllText(finfo.FullName, sb.ToString());

            _LoadDocument(finfo.FullName);
        }


        private static string _SelectDirectoryDialog()
        {
            using(var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.AddToRecent = true;
                dlg.ShowNewFolderButton = false;
                // dlg.ClientGuid =

                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return null;

                return dlg.SelectedPath;
            }
        }
    }
}
