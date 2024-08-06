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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Interaction logic for PackagesList.xaml
    /// </summary>
    public partial class PackagesList : UserControl
    {
        public PackagesList()
        {
            InitializeComponent();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                if (fe.DataContext is PackageMVVM package)
                {
                    var url = "https://www.nuget.org/packages/" + package.Name;

                    var psi = new System.Diagnostics.ProcessStartInfo(url);
                    psi.UseShellExecute = true;

                    System.Diagnostics.Process.Start(psi);
                }
            }            
        }

        private void CheckBox_UsedByPrjs(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb)
            {
                myUsedByProjectsColumn.Visibility = cb.IsChecked ?? true ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void CheckBox_Frameworks(object sender, RoutedEventArgs e)
        {
            myFrameworksColumn.Visibility = _Visibility(sender as DependencyObject);
        }

        private void CheckBox_Tags(object sender, RoutedEventArgs e)
        {
            myTagsColumn.Visibility = _Visibility(sender as DependencyObject);
        }

        private void CheckBox_Summary(object sender, RoutedEventArgs e)
        {
            mySummaryColumn.Visibility = _Visibility(sender as DependencyObject);
        }

        private void CheckBox_Description(object sender, RoutedEventArgs e)
        {
            myDescriptionColumn.Visibility = _Visibility(sender as DependencyObject);
        }

        private void CheckBox_ProjectUrl(object sender, RoutedEventArgs e)
        {
            myProjectUrlColumn.Visibility = _Visibility(sender as DependencyObject);
        }

        public static Visibility _Visibility(DependencyObject dep)
        {
            return _IsChecked(dep) ? Visibility.Visible : Visibility.Collapsed;
        }

        public static bool _IsChecked(DependencyObject dep)
        {
            switch(dep)
            {
                case CheckBox cb: return cb.IsChecked == true;
                case MenuItem mi: return mi.IsChecked == true;
                default: return false;
            }
        }
    }
}
