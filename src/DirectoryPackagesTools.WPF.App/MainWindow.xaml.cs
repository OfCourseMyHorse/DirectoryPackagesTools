using System;
using System.Collections.Generic;
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
    public partial class MainWindow : Window, IProgress<int>
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void Report(int value)
        {
            this.Dispatcher.Invoke( ()=> myProgressBar.Value= value);
        }

        private void MenuItem_Load(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.RestoreDirectory = true;
            dlg.Filter = "Package Versions|directory.packages.props";

            if (!dlg.ShowDialog().Value) return;

            myProgressBar.Visibility = Visibility.Visible;

            void _loadDocument()
            {
                var props = PropsMVVM
                    .Load(dlg.FileName, this)
                    .ConfigureAwait(true)
                    .GetAwaiter()
                    .GetResult();

                this.Dispatcher.Invoke(() => { this.DataContext = props; myProgressBar.Visibility = Visibility.Collapsed; });
            }

            Task.Run(_loadDocument);
        }

        private void MenuItem_Save(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is PropsMVVM mvvm) mvvm.Save();
        }
    }
}
