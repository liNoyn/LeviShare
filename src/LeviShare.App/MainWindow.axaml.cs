using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace LeviShare.App;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    public void ShowError(string text) => _vm.ShowMessage(_vm.L.Format("Error", text), "bad");

    void OnJapanese(object? sender, RoutedEventArgs e) => _vm.SetLanguage(true);

    void OnEnglish(object? sender, RoutedEventArgs e) => _vm.SetLanguage(false);

    void OnReload(object? sender, RoutedEventArgs e) => _vm.Reload();

    async void OnChangeFolder(object? sender, RoutedEventArgs e)
    {
        var start = Directory.Exists(_vm.DataRoot) ? await StorageProvider.TryGetFolderFromPathAsync(_vm.DataRoot) : null;
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = _vm.L["DataFolder"],
            SuggestedStartLocation = start,
            AllowMultiple = false,
        });
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path) _vm.SetDataRoot(path);
    }

    void OnSelectAll(object? sender, RoutedEventArgs e) => ((sender as Control)?.DataContext as ShareSection)?.SetAll(true);

    void OnSelectNone(object? sender, RoutedEventArgs e) => ((sender as Control)?.DataContext as ShareSection)?.SetAll(false);

    async void OnExport(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _vm.L["SaveTitle"],
            SuggestedFileName = _vm.SuggestedFileName,
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType("Zip") { Patterns = ["*.zip"] }],
        });
        if (file?.TryGetLocalPath() is { } path) await _vm.ExportAsync(path);
    }

    void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        if (_vm.LastExport is not { } path || !File.Exists(path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    async void OnCopySteps(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is null) return;
        await Clipboard.SetTextAsync(_vm.ImportSteps);
        _vm.ShowMessage(_vm.L["Copied"], "ok");
    }
}
