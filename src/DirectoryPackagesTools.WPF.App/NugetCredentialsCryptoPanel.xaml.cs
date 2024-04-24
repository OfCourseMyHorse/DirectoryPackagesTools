using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

using DirectoryPackagesTools.Client;

using Microsoft.Win32;

using NuGet.Configuration;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Interaction logic for NugetCredentialsCryptoPanel.xaml
    /// </summary>
    public partial class NugetCredentialsCryptoPanel : UserControl
    {
        public static void ShowDialog(Window wnd)
        {            
            var window = new Window();
            window.Owner = wnd;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            window.ShowInTaskbar = false;
            window.SizeToContent= SizeToContent.WidthAndHeight;
            
            window.Title = "Nuget Clear/Encrypted password converter";
            
            window.Content = new NugetCredentialsCryptoPanel();

            window.ShowDialog();
        }

        public NugetCredentialsCryptoPanel()
        {
            InitializeComponent();
        }

        private void _OnClick_Encrypt(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(myClearText.Text)) { myEncryptedText.Text = string.Empty; return; }

            myEncryptedText.Text = EncryptionUtility.EncryptString(myClearText.Text);
        }

        private void _OnClick_Dencrypt(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(myEncryptedText.Text)) { myClearText.Text = string.Empty; return; }

            myClearText.Text = EncryptionUtility.DecryptString(myEncryptedText.Text);
        }

        private void _OnClick_ConcealPasswords(object sender, RoutedEventArgs e)
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

        private bool ConfirmAction(string msg)
        {
            var wnd = Window.GetWindow(this);

            var r = MessageBox.Show(wnd, msg, "Confirm Action", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            return r == MessageBoxResult.OK;
        }
    }
}
