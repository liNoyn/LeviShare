using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace LeviShare.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // UI-thread errors are logged and shown instead of closing the app.
        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            var path = Program.WriteCrashLog(e.Exception);
            e.Handled = true;
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow w })
                w.ShowError($"{e.Exception.Message}\n{path}");
        };
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
