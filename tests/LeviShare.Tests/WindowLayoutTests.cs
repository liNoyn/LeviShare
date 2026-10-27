using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using LeviShare.App;

[assembly: AvaloniaTestApplication(typeof(LeviShare.Tests.UiTestApp))]

namespace LeviShare.Tests;

public static class UiTestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App.App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class WindowLayoutTests
{
    [AvaloniaTheory]
    [InlineData(960, 640, false, false)]
    [InlineData(960, 640, false, true)]
    [InlineData(960, 640, true, false)]
    [InlineData(960, 640, true, true)]
    [InlineData(1180, 820, false, false)]
    [InlineData(1180, 820, false, true)]
    [InlineData(1180, 820, true, false)]
    [InlineData(1180, 820, true, true)]
    public void EssentialControlsFitInBothThemesAndLanguages(int width, int height, bool japanese, bool dark)
    {
        var vm = new MainViewModel(new LauncherData(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        vm.SetLanguage(japanese);
        var window = new MainWindow(vm) { Width = width, Height = height };
        try
        {
            window.Show();
            var themePicker = window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Items.Count == 3);
            themePicker.SelectedIndex = dark ? 2 : 1;
            window.UpdateLayout();
            // Headless windows have a fixed 1024x768 client. Exercise the actual
            // content tree at each requested size independently of that virtual host.
            var layout = Assert.IsAssignableFrom<Control>(window.Content);
            layout.Measure(new Size(width, height));
            layout.Arrange(new Rect(0, 0, width, height));
            Assert.Equal(new Size(width, height), layout.Bounds.Size);
            Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
            Assert.Equal(Color.Parse(dark ? "#09090B" : "#FAFAFA"), Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);

            var search = window.FindControl<TextBox>("SearchBox")!;
            var controls = window.GetVisualDescendants().OfType<Button>()
                .Where(b => Equals(b.Content, vm.L["Export"]) || Equals(b.Content, vm.L["CopySteps"]) ||
                            Equals(b.Content, vm.L["Change"]) || Equals(b.Content, vm.L["Reload"]))
                .Cast<Control>().Append(search).Append(themePicker).ToList();
            Assert.Equal(6, controls.Count);
            foreach (var control in controls)
            {
                var position = control.TranslatePoint(default, layout)!.Value;
                Assert.True(control.Bounds.Width > 40, $"{control} has collapsed");
                Assert.True(position.X >= 0 && position.X + control.Bounds.Width <= width + 1,
                    $"{control}: X={position.X}, width={control.Bounds.Width}, layout={layout.Bounds}");
                Assert.True(position.Y >= 0 && position.Y + control.Bounds.Height <= height + 1,
                    $"{control}: Y={position.Y}, height={control.Bounds.Height}, layout={layout.Bounds}");
            }
            Assert.False(vm.CanExport);
            Assert.False(controls.OfType<Button>().Single(b => Equals(b.Content, vm.L["Export"])).IsEnabled);
        }
        finally { window.Close(); }
    }
}
