using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Clc.Sip.Desktop.ViewModels;
using Clc.Sip.Desktop.Views;

namespace Clc.Sip.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var model = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow { DataContext = model };
            desktop.MainWindow.Opened += async (_, _) => await model.InitializeAsync();
            desktop.Exit += (_, _) => model.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
