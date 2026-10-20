using System.Text;
using Avalonia;
using Avalonia.Media;

namespace LeviShare.App;

internal static class Program
{
    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LeviShare", "logs");

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrashLog(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { WriteCrashLog(e.Exception); e.SetObserved(); };
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new FontManagerOptions { DefaultFamilyName = "Yu Gothic UI" })
            .LogToTrace();

    /// <summary>Writes unexpected errors to %LOCALAPPDATA%\LeviShare\logs so problems can be reported.</summary>
    public static string? WriteCrashLog(Exception? ex)
    {
        if (ex is null) return null;
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(path, $"LeviShare {ShareExporter.ToolVersion}\n{Environment.OSVersion}\n{DateTime.Now:O}\n\n{ex}", new UTF8Encoding(false));
            return path;
        }
        catch { return null; }
    }
}
