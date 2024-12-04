using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings;
using Avalonia.Media;

namespace DirectoryPackagesTools
{
    internal static class _AvaloniaLifecycles
    {
        public static void BindToVisibilityOf(this ToggleButton checkbox, Visual control)
        {
            //https://github.com/AvaloniaUI/Avalonia/discussions/13678
            // https://github.com/AvaloniaUI/Avalonia/discussions/13555

            var source1 = control.GetObservable(Visual.IsVisibleProperty);
            checkbox.Bind(ToggleButton.IsCheckedProperty, source1.ToBinding());

            var source2 = checkbox.GetObservable(ToggleButton.IsCheckedProperty);
            control.Bind(Visual.IsVisibleProperty, source2.ToBinding());
        }

        public static void BindToVisibilityOf(this ToggleButton checkbox, DataGridColumn control)
        {
            // how the heck do we handle two way binding??

            var source1 = control.GetObservable<bool,bool?>(DataGridColumn.IsVisibleProperty, a => a);
            checkbox.Bind(ToggleButton.IsCheckedProperty, source1.ToBinding());

            var source2 = checkbox.GetObservable<bool?,bool>(ToggleButton.IsCheckedProperty, a=> a ?? true);
            control.Bind(DataGridColumn.IsVisibleProperty, source2.ToBinding());
        }

        public static bool TryShutDown(this Avalonia.Application app, int exitCode = 0)
        {
            if (app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.TryShutdown(exitCode);
            }

            return false;
        }

        // https://github.com/AvaloniaUI/Avalonia/discussions/14485

        public static async Task<TResult> OpenPopupAsync<TResult>(this Control parent, Control content, TResult defVal, params (string,TResult)[] buttons)
        {
            (Button,TResult) _createBtn((string title, TResult result) pair)
            {
                var btn = new Button()
                { 
                    Content = pair.title,
                    Margin= new Avalonia.Thickness(4,1,4,1)
                };

                return (btn, pair.result);
            }

            var btns = buttons.Select(_createBtn).ToArray();

            var buttonsRow = new StackPanel();
            buttonsRow.Orientation = Avalonia.Layout.Orientation.Horizontal;
            buttonsRow.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            buttonsRow.Children.AddRange(btns.Select(item => item.Item1));            

            var grid = new Grid();
            grid.RowDefinitions = new RowDefinitions("* Auto");

            Grid.SetRow(content, 0);
            grid.Children.Add(content);

            Grid.SetRow(buttonsRow, 1);
            grid.Children.Add(buttonsRow);

            return await OpenPopupAsync(parent, grid, defVal, btns);
        }

        public static Task<TResult> OpenPopupAsync<TResult>(this Control parent, Control content, TResult defVal, params (Button, TResult)[] buttons)
        {
            var frame = new Border
            {                
                Padding = new Avalonia.Thickness(10),
                Background = Brushes.LightGray,
                BorderThickness = new Avalonia.Thickness(1),
                BorderBrush = Brushes.Black,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                // Width = 400,
                // Height = 300,
                Child = content
            };


            // we need to wrap the actual frame with an extra wide frame so the shadow can work
            frame.BoxShadow = new BoxShadows(BoxShadow.Parse("1 4 8 0 #50000000"));
            frame = new Border() 
            {
                Padding = new Avalonia.Thickness(10),
                Child = frame
            };

            var parentStatus = parent.IsHitTestVisible;
            parent.IsHitTestVisible = false; // disable parent interactivity

            var popup = AddPopup(parent, frame);
            popup.Tag = defVal;            

            foreach (var (b, r) in buttons)
            {
                b.Click += (s, e) => { popup.Tag = r; popup.Close(); };
            }

            var resultWaiter = new System.Threading.Tasks.TaskCompletionSource<TResult>();

            popup.Closed += (s, e) =>
            {
                ((ISetLogicalParent)popup).SetParent(null);
                resultWaiter.SetResult((TResult)popup.Tag);

                parent.IsHitTestVisible = parentStatus; // set parent status back
            };

            return resultWaiter.Task;            
        }

        public static Popup AddPopup(Control parent, Control content)
        {
            // Create the Popup control
            var popup = new Popup
            {
                PlacementTarget = parent,                
                Placement = PlacementMode.Center,
                PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.TopLeft,
                
                // PlacementMode = PlacementMode.Bottom,
                IsLightDismissEnabled = true,
                // StaysOpen = false,
                Child = content
            };

            // Add the Popup to the parent control's logical children
            ((ISetLogicalParent)popup).SetParent(parent);

            // Open the Popup
            popup.IsOpen = true;

            return popup;
        }        
    }
}
