using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DirectoryPackagesTools
{
    public partial class MainWindow : Window
    {
        #region lifecycle

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += _OnLoaded;
        }

        private async void _OnLoaded(object? sender, RoutedEventArgs e)
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
            #region lifecycle
            public _BackgroundTaskMonitor(MainWindow window)
            {
                _Window = window;
                _TokenSource = new CancellationTokenSource();

                _Window.myClientArea.IsEnabled = false;
                _Window.myProgressBar.IsVisible = true;

                _Window.myCancelBtn.Click += MyCancelBtn_Click;
                _Window.myCancelBtn.IsVisible = true;
            }

            public void Dispose()
            {
                Avalonia.Threading.Dispatcher.UIThread.Invoke(_RestoreWindow);

                _TokenSource.Dispose();
            }

            private void _RestoreWindow()
            {
                _Window.myClientArea.IsEnabled = true;
                _Window.myProgressBar.IsVisible = false;

                _Window.myCancelBtn.Click -= MyCancelBtn_Click;
                _Window.myCancelBtn.IsVisible = false;
            }

            #endregion

            #region data

            private readonly MainWindow _Window;
            private readonly CancellationTokenSource _TokenSource;

            public CancellationToken Token => _TokenSource.Token;

            #endregion

            #region API

            public void Report(int value)
            {
                var wnd = _Window;

                void _setProgress()
                {
                    if (value >= 0) { wnd.myProgressBar.Value = value; wnd.myProgressBar.IsIndeterminate = false; }
                    else { wnd.myProgressBar.Value = 0; wnd.myProgressBar.IsIndeterminate = true; }
                }

                Avalonia.Threading.Dispatcher.UIThread.Invoke(_setProgress);
            }

            public void Report(Exception value)
            {
                #if !DISABLE_TRYCATCH
                System.Diagnostics.Debug.Fail(value.Message);
                #endif

                throw new NotImplementedException();
                // Avalonia.Threading.Dispatcher.UIThread.Invoke(_setProgress);
                // _Window.Dispatcher.Invoke(() => MessageBox.Show(value.Message, "Error"));
            }

            private void MyCancelBtn_Click(object sender, RoutedEventArgs e)
            {
                _TokenSource.Cancel();
            }

            #endregion
        }

        #endregion

        #region API

        private async void MenuItem_Load(object? sender, RoutedEventArgs e)
        {
            var finfo = await this.TryOpenForRead(
                ("Package Versions", "directory.packages.props"),
                ("Dotnet Tools Versions", "dotnet-tools.json"),
                ("properties file", "*.props"))
                .CastResultTo<System.IO.FileInfo>();

            if (finfo == null) return;
            
            await _LoadDocumentAsync(finfo.FullName).ConfigureAwait(false);
        }

        private async Task _LoadDocumentAsync(string documentPath)
        {
            using var ctx = BeginTask();

            #if !DISABLE_TRYCATCH
            try
            {
            #endif
                var doc = await PackagesVersionsProjectMVVM
                    .LoadAsync(documentPath, ctx, ctx.Token)
                    .ConfigureAwait(true);

                if (doc == null) return;

                this.DataContext = doc;
                this.Title = "Directory Packages Manager - " + documentPath;

            #if !DISABLE_TRYCATCH
            }
            catch (OperationCanceledException) { this.MessageBox().Show("Load cancelled."); }
            catch (Exception ex)
            {
                this.MessageBox().Show(ex.Message, "Error");
            }
            #endif
        }

        private void MenuItem_Save(object? sender, RoutedEventArgs e)
        {
            MVVMContext?.Save();
        }

        private void MenuItem_SaveAndCommit(object? sender, RoutedEventArgs e)
        {
            MenuItem_Save(sender, e);

            var finfo = MVVMContext?.File;
            if (finfo == null) return;

            if (finfo.Directory.CommitToVersionControl()) Avalonia.Application.Current.TryShutDown();
        }

        private void MenuItem_OpenCommandLine(object? sender, RoutedEventArgs e)
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


        private async void MenuItem_ShowNugetPasswordsManagerDialog(object? sender, RoutedEventArgs e)
        {
            await NugetCredentialsCryptoPanel.ShowDialog(this);
        }        

        private async void MenuItem_New(object? sender, RoutedEventArgs e)
        {
            var dir = await this.OpenFolderPicker().CastResultTo<System.IO.DirectoryInfo>();
            if (dir == null) return;

            var finfo = new System.IO.FileInfo(System.IO.Path.Combine(dir.FullName, "Directory.Packages.props"));
            if (finfo.Exists)
            {
                if (await this.MessageBox().Show("Overwrite?", "File already exists", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            }

            var r = await this.MessageBox().Show("Remove Version='xxx' from csproj files?", "Action", MessageBoxButton.YesNoCancel);
            if (r == MessageBoxResult.Cancel) return;

            PackagesVersionsProjectMVVM.WriteNewVersionsProject(finfo, r == MessageBoxResult.Yes);

            await _LoadDocumentAsync(finfo.FullName);
        }        

        private async void _MenuItem_RestoreVersionsToProjects(object? sender, RoutedEventArgs e)
        {
            if (! await ConfirmAction("Restore versions back to csprojs? (Cannot be reverted)")) return;

            MVVMContext?.RestoreVersionsToProjects();
        }

        private async void _MenuItem_QueryPackageDependencies(object? sender, RoutedEventArgs e)
        {
            if (MVVMContext == null) return;

            using var ctx = BeginTask();

            await MVVMContext
                .RefreshPackageDependenciesAsync(ctx, ctx.Token)
                .ConfigureAwait(true);
        }

        private async Task<bool> ConfirmAction(string msg)
        {
            var r = await this.MessageBox().Show(msg, "Confirm Action", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            return r == MessageBoxResult.OK;
        }

        private async void _MenuItem_About(object? sender, RoutedEventArgs e)
        {
            var r = await this.MessageBox().Show("This tool is used to update package versions\r\nof a Directory.Packages.props file.", "About Directory Packages tools", MessageBoxButton.OK, MessageBoxImage.Question);                        
        }

        #endregion
    }
}