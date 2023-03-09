﻿using System;
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
    }
}
