using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DirectoryPackagesTools.Client;

using NuGet.Configuration;

namespace DirectoryPackagesTools;

public partial class NugetCredentialsCryptoPanel : UserControl
{
    #region lifecycle
    public static async Task ShowDialog(Window wnd)
    {
        var window = new Window();
        
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        window.ShowInTaskbar = false;
        window.SizeToContent = SizeToContent.WidthAndHeight;

        window.Title = "Nuget Clear/Encrypted password converter";

        window.Content = new NugetCredentialsCryptoPanel();

        await window.ShowDialog(wnd);
    }

    private NugetCredentialsCryptoPanel()
    {
        InitializeComponent();
    }

    #endregion

    #region data

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

    private async void _OnClick_ConcealPasswords(object sender, RoutedEventArgs e)
    {
        var finfo = await this.TryOpenForRead("Nuget.Config files", "*.config").CastResultTo<System.IO.FileInfo>();
        if (finfo == null) return;        

        if (!await ConfirmAction("Conceal passwords? (Cannot be reverted)")) return;

        var r = CredentialsUtils.EncryptNugetConfigClearTextPasswords(finfo);

        await this.MessageBox().Show(r ? "passwords concealed" : "no clear passwords found", "result");        
    }

    private async Task<bool> ConfirmAction(string msg)
    {
        var r = await this.MessageBox().Show(msg, "Confirm Action", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        return r == MessageBoxResult.OK;
    }

    #endregion
}