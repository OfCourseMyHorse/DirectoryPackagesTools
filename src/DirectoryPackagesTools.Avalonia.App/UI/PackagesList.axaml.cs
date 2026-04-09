using System.Collections.Generic;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

namespace DirectoryPackagesTools;

public partial class PackagesList : UserControl
{
    #region lifecycle
    public PackagesList()
    {
        InitializeComponent();        
    }

    #endregion

    #region Packages Source

    public static readonly DirectProperty<PackagesList, IEnumerable<PackageMVVM>> PackagesSourceProperty
        = AvaloniaProperty.RegisterDirect<PackagesList, IEnumerable<PackageMVVM>>
        (
            nameof(PackagesSource),
            ctrl => ctrl.PackagesSource,
            (ctrl, val) => ctrl.PackagesSource = val);

    private IEnumerable<PackageMVVM> _PackagesSource;

    public IEnumerable<PackageMVVM> PackagesSource
    {
        get => _PackagesSource;
        set
        {
            if (this.SetAndRaise(PackagesSourceProperty, ref _PackagesSource, value))
            {
                myPackages.ItemsSource = _PackagesSource;
            }
        }
    }

    #endregion

    #region events

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is StyledElement se)
        {
            if (se.DataContext is PackageMVVM package)
            {
                var url = "https://www.nuget.org/packages/" + package.Name;

                var psi = new System.Diagnostics.ProcessStartInfo(url);
                psi.UseShellExecute = true;

                System.Diagnostics.Process.Start(psi);
            }
        }
    }

    #endregion
}