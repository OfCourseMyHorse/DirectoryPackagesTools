using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DirectoryPackagesTools;

public partial class PackagesList : UserControl
{
    #region lifecycle
    public PackagesList()
    {
        InitializeComponent();

        myPackages.Columns.CollectionChanged += (s, e) => _UpdateColumnsVisibility();
    }

    private void _UpdateColumnsVisibility()
    {
        myVisibility.ItemsSource = myPackages.Columns.Select(c => new TableViewColumnVisibleViewModel(c));
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
                _UpdateColumnsVisibility();
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

public class TableViewColumnVisibleViewModel : ObservableObject
{
    public TableViewColumnVisibleViewModel(TableViewColumn tvc)
    {
        _Column = tvc;
    }

    public TableViewColumn _Column;


    public Object? Header => _Column.Header;
    

    public bool IsVisible
    {
        get => _Column.Width.IsAuto || _Column.Width.IsStar || _Column.Width.Value > 0f;
        set
        {
            _Column.Width = value ? GridLength.Star : new GridLength(0);
            OnPropertyChanged(nameof(IsVisible));
        }
    }
}


class BoolToColumnWidth : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Boolean vbool)
        {
            if (targetType == typeof(GridLength))
            {
                return vbool
                    ? GridLength.Star
                    : new GridLength(0);
            }
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}