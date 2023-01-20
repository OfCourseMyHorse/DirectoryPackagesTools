﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
            myClientArea.IsEnabled = false;
            myProgressBar.Visibility = Visibility.Visible;            

            var cts = new CancellationTokenSource();

            void _cancelBtn(object sender, RoutedEventArgs e) { cts.Cancel(); }

            myCancelBtn.Click += _cancelBtn;            
            myCancelBtn.Visibility = Visibility.Visible;

            void _restoreWindow()
            {
                myClientArea.IsEnabled = true;
                myProgressBar.Visibility = Visibility.Collapsed;

                myCancelBtn.Click -= _cancelBtn;
                myCancelBtn.Visibility = Visibility.Collapsed;

                cts.Dispose();
            }

            void _loadDocument()
            {
                PackagesVersionsProjectMVVM versions = null;

                try
                {
                    versions = PackagesVersionsProjectMVVM
                        .Load(documentPath, this, cts.Token)
                        .ConfigureAwait(true)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (OperationCanceledException ex)
                {
                    MessageBox.Show("Load cancelled.");
                }
                finally
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        if (versions != null) this.DataContext = versions;

                        _restoreWindow();
                    });
                }                
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

            var r = MessageBox.Show("Remove Version='xxx' from csproj files?", "Action", MessageBoxButton.YesNoCancel);
            if (r == MessageBoxResult.Cancel) return;

            XmlPackagesVersionsProjectDOM.CreateVersionFileFromExistingProjects(finfo);

            if (r == MessageBoxResult.Yes)
            {
                XmlProjectDOM.RemoveVersionsFromProjectsFiles(finfo.Directory);                
            }

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
