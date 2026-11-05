using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Clc.Sip.Desktop.Views;

internal sealed class ConfirmActionWindow : Window
{
    public ConfirmActionWindow(string title, string message, string action)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.Bind(BackgroundProperty, this.GetResourceObservable("SurfaceBrush"));
        var cancel = new Button { Name = "CancelButton", Content = "Cancel", IsDefault = true, IsCancel = true, MinWidth = 80 };
        var confirm = new Button { Name = "ConfirmButton", Content = action, MinWidth = 80 };
        cancel.Click += (_, _) => Close(false);
        confirm.Click += (_, _) => Close(true);
        Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { cancel, confirm } }
            }
        };
        Opened += (_, _) => cancel.Focus(NavigationMethod.Tab);
    }
}
