using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DirectoryPackagesTools
{
    /// <summary>
    /// Represents a context that can be used to open a MessageBox dialog
    /// </summary>
    internal class _AvaloniaMessageBox
    {
        #region lifecycle
        internal _AvaloniaMessageBox(Avalonia.Controls.Control control)
        {
            _Control = control;
        }

        #endregion

        #region data

        private readonly Avalonia.Controls.Control _Control;

        #endregion

        #region API

        public async Task<MessageBoxResult> Show(string text)
        {
            var dsp = Avalonia.Threading.Dispatcher.UIThread;

            if (!dsp.CheckAccess())
            {
                return await dsp.InvokeAsync<MessageBoxResult>(async () => await Show(text));                
            }

            var (content, buttons) = _CreateContent(text, string.Empty, MessageBoxButton.OK, MessageBoxImage.None);
            return await _Open(content, buttons);
        }

        public async Task<MessageBoxResult> Show(string text, string title)
        {
            var dsp = Avalonia.Threading.Dispatcher.UIThread;

            if (!dsp.CheckAccess())
            {
                return await dsp.InvokeAsync<MessageBoxResult>(async () => await Show(text, title));
            }

            var (content, buttons) = _CreateContent(text, title, MessageBoxButton.OK, MessageBoxImage.None);
            return await _Open(content, buttons);
        }

        public async Task<MessageBoxResult> Show(string text, string title, MessageBoxButton button)
        {
            var dsp = Avalonia.Threading.Dispatcher.UIThread;

            if (!dsp.CheckAccess())
            {
                return await dsp.InvokeAsync<MessageBoxResult>(async () => await Show(text, title, button));
            }

            var (content, buttons) = _CreateContent(text, title, button, MessageBoxImage.None);
            return await _Open(content, buttons);
        }

        public async Task<MessageBoxResult> Show(string text, string title, MessageBoxButton button, MessageBoxImage image)
        {
            var dsp = Avalonia.Threading.Dispatcher.UIThread;

            if (!dsp.CheckAccess())
            {
                return await dsp.InvokeAsync<MessageBoxResult>(async () => await Show(text, title, button, image));
            }

            var (content, buttons) = _CreateContent(text, title, button, image);
            return await _Open(content, buttons);
        }

        #endregion

        #region core        

        private (Control content, (string,MessageBoxResult)[] buttons) _CreateContent(string text, string title, MessageBoxButton button, MessageBoxImage image)
        {
            var buttonsList = new List<(string, MessageBoxResult)>();

            if (button == MessageBoxButton.OK || button == MessageBoxButton.OKCancel)
            {                
                buttonsList.Add(("OK", MessageBoxResult.OK));
            }

            if (button == MessageBoxButton.YesNo || button == MessageBoxButton.YesNoCancel)
            {                
                buttonsList.Add(("Yes", MessageBoxResult.Yes));                
                buttonsList.Add(("No", MessageBoxResult.No));
            }

            if (button == MessageBoxButton.OKCancel || button == MessageBoxButton.YesNoCancel)
            {                
                buttonsList.Add(("Cancel", MessageBoxResult.Cancel));
            }

            var titleRow = new TextBlock(); Grid.SetRow(titleRow, 0);
            titleRow.Text = title;
            titleRow.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;

            var contentRow = new TextBlock(); Grid.SetRow(contentRow, 1);
            contentRow.Text = text;
            contentRow.TextWrapping = TextWrapping.Wrap;
            contentRow.Margin = new Thickness(8);
            contentRow.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;                       

            var grid = new Grid();
            grid.RowDefinitions = new RowDefinitions("Auto *");
            grid.Children.Add(titleRow);
            grid.Children.Add(contentRow);                    

            return (grid, buttonsList.ToArray());
        }

        private async Task<MessageBoxResult> _Open(Control content, params (string, MessageBoxResult)[] buttons)
        {
            return await _Control.OpenPopupAsync(content, MessageBoxResult.Cancel, buttons);
        }

        #endregion
    }

    public enum MessageBoxButton
    {
        //
        // Summary:
        //     The message box displays an OK button.
        OK = 0,
        //
        // Summary:
        //     The message box displays OK and Cancel buttons.
        OKCancel = 1,
        //
        // Summary:
        //     The message box displays Yes, No, and Cancel buttons.
        YesNoCancel = 3,
        //
        // Summary:
        //     The message box displays Yes and No buttons.
        YesNo = 4
    }

    public enum MessageBoxImage
    {
        //
        // Summary:
        //     The message box contains no symbols.
        None = 0,
        //
        // Summary:
        //     The message box contains a symbol consisting of white X in a circle with a red
        //     background.
        Error = 16,
        //
        // Summary:
        //     The message box contains a symbol consisting of a white X in a circle with a
        //     red background.
        Hand = 16,
        //
        // Summary:
        //     The message box contains a symbol consisting of white X in a circle with a red
        //     background.
        Stop = 16,
        //
        // Summary:
        //     The message box contains a symbol consisting of a question mark in a circle.
        //     The question mark message icon is no longer recommended because it does not clearly
        //     represent a specific type of message and because the phrasing of a message as
        //     a question could apply to any message type. In addition, users can confuse the
        //     question mark symbol with a help information symbol. Therefore, do not use this
        //     question mark symbol in your message boxes. The system continues to support its
        //     inclusion only for backward compatibility.
        Question = 32,
        //
        // Summary:
        //     The message box contains a symbol consisting of an exclamation point in a triangle
        //     with a yellow background.
        Exclamation = 48,
        //
        // Summary:
        //     The message box contains a symbol consisting of an exclamation point in a triangle
        //     with a yellow background.
        Warning = 48,
        //
        // Summary:
        //     The message box contains a symbol consisting of a lowercase letter i in a circle.
        Asterisk = 64,
        //
        // Summary:
        //     The message box contains a symbol consisting of a lowercase letter i in a circle.
        Information = 64
    }

    public enum MessageBoxResult
    {
        //
        // Summary:
        //     The message box returns no result.
        None = 0,
        //
        // Summary:
        //     The result value of the message box is OK.
        OK = 1,
        //
        // Summary:
        //     The result value of the message box is Cancel.
        Cancel = 2,
        //
        // Summary:
        //     The result value of the message box is Yes.
        Yes = 6,
        //
        // Summary:
        //     The result value of the message box is No.
        No = 7
    }

    internal static class _AvaloniaMessageBoxExtensions
    {
        public static _AvaloniaMessageBox MessageBox(this Control ctrl)
        {
            return new _AvaloniaMessageBox(ctrl);
        }
    }
}
