using System;
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

using DirectoryPackagesTools.Client;
using DirectoryPackagesTools.DOM;
using Microsoft.Win32;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region lifecycle

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += _OnLoaded;
        }

        private async void _OnLoaded(object sender, RoutedEventArgs e)
        {
            var path = Environment
                .GetCommandLineArgs()
                .Where(item => item.EndsWith("Directory.Packages.Props", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(path)) await _LoadDocumentAsync(path);            
        }

        #endregion

        #region data

        private PackagesVersionsProjectMVVM MVVMContext => this.DataContext as PackagesVersionsProjectMVVM;

        #endregion

        #region background tasks

        private _BackgroundTaskMonitor BeginTask()
        {
            return new _BackgroundTaskMonitor(this);
        }       

        private readonly struct _BackgroundTaskMonitor : IDisposable, IProgress<int>, IProgress<Exception>
        {
            public _BackgroundTaskMonitor(MainWindow window)
            {
                _Window = window;
                _TokenSource = new CancellationTokenSource();

                _Window.myClientArea.IsEnabled = false;
                _Window.myProgressBar.Visibility = Visibility.Visible;

                _Window.myCancelBtn.Click += MyCancelBtn_Click;
                _Window.myCancelBtn.Visibility = Visibility.Visible;
            }

            public void Dispose()
            {
                _Window.Dispatcher.Invoke(_RestoreWindow);

                _TokenSource.Dispose();
            }

            private void _RestoreWindow()
            {
                _Window.myClientArea.IsEnabled = true;
                _Window.myProgressBar.Visibility = Visibility.Collapsed;

                _Window.myCancelBtn.Click -= MyCancelBtn_Click;
                _Window.myCancelBtn.Visibility = Visibility.Collapsed;
            }

            private readonly MainWindow _Window;
            private readonly CancellationTokenSource _TokenSource;

            public CancellationToken Token => _TokenSource.Token;

            public void Report(int value)
            {
                var wnd = _Window;

                void _setProgress()
                {
                    if (value >= 0) { wnd.myProgressBar.Value = value; wnd.myProgressBar.IsIndeterminate = false; }
                    else { wnd.myProgressBar.Value = 0; wnd.myProgressBar.IsIndeterminate = true; }
                }

                _Window.Dispatcher.Invoke(_setProgress);
            }

            public void Report(Exception value)
            {
                _Window.Dispatcher.Invoke(() => MessageBox.Show(value.Message, "Error"));
            }

            private void MyCancelBtn_Click(object sender, RoutedEventArgs e)
            {
                _TokenSource.Cancel();
            }            
        }

        #endregion

        #region API

        private async void MenuItem_Load(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.RestoreDirectory = true;
            dlg.Filter = "Package Versions|directory.packages.props|Dotnet Tools Versions|dotnet-tools.json";

            if (!dlg.ShowDialog().Value) return;
            await _LoadDocumentAsync(dlg.FileName);
        }

        private async Task _LoadDocumentAsync(string documentPath)
        {
            using var ctx = BeginTask();

            try
            {
                this.DataContext = await PackagesVersionsProjectMVVM
                    .LoadAsync(documentPath, ctx, ctx.Token)
                    .ConfigureAwait(true);

                this.Title = "Directory Packages Manager - " + documentPath;
            }            
            catch (OperationCanceledException) { MessageBox.Show("Load cancelled."); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
        }

        private void MenuItem_Save(object sender, RoutedEventArgs e)
        {
            MVVMContext?.Save();
        }

        private void MenuItem_SaveAndCommit(object sender, RoutedEventArgs e)
        {
            MenuItem_Save(sender, e);

            var finfo = MVVMContext?.File;
            if (finfo == null) return;            

            if (finfo.Directory.CommitToVersionControl()) Application.Current.Shutdown();            
        }        

        private void MenuItem_OpenCommandLine(object sender, RoutedEventArgs e)
        {
            var finfo = MVVMContext?.File;
            if (finfo == null) return;

            _OpenCommandLine(finfo.Directory);
        }

        private static void _OpenCommandLine(DirectoryInfo dinfo)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe");
            psi.UseShellExecute = true;
            psi.WorkingDirectory = dinfo.FullName;

            System.Diagnostics.Process.Start(psi);
        }


        private void MenuItem_ConcealPasswords(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.RestoreDirectory = true;
            dlg.Filter = "Nuget.Config files|*.config";

            if (dlg.ShowDialog() != true) return;

            var finfo = new System.IO.FileInfo(dlg.FileName);

            if (!ConfirmAction("Conceal passwords? (Cannot be reverted)")) return;

            var r = CredentialsUtils.EncryptNugetConfigClearTextPasswords(finfo);

            MessageBox.Show(r ? "passwords concealed" : "no clear passwords found", "result");
        }

        private async void MenuItem_New(object sender, RoutedEventArgs e)
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

            PackagesVersionsProjectMVVM.WriteNewVersionsProject(finfo, r == MessageBoxResult.Yes);

            await _LoadDocumentAsync(finfo.FullName);
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

        private void _MenuItem_RestoreVersionsToProjects(object sender, RoutedEventArgs e)
        {
            if (!ConfirmAction("Restore versions back to csprojs? (Cannot be reverted)")) return;

            MVVMContext?.RestoreVersionsToProjects();
        }

        private async void _MenuItem_QueryPackageDependencies(object sender, RoutedEventArgs e)
        {
            if (MVVMContext == null) return;

            using var ctx = BeginTask();

            await MVVMContext
                .RefreshPackageDependenciesAsync(ctx, ctx.Token)
                .ConfigureAwait(true);
        }

        private bool ConfirmAction(string msg)
        {
            var r = MessageBox.Show(this, msg, "Confirm Action", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            return r == MessageBoxResult.OK;
        }

        #endregion
    }    
}
