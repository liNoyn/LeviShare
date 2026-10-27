using LeviShare.App;

namespace LeviShare.Tests;

public sealed class MainViewModelTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "LeviShare-view-tests-" + Guid.NewGuid().ToString("N"));

    MainViewModel CreateViewModel()
    {
        var instance = Path.Combine(_root, "versions", "Example");
        Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "version.json"),
            """{"gameVersion":"1.26.51","type":"release","packageType":"gdk","enableIsolation":true}""");
        foreach (var name in new[] { "Alpha", "Beta" })
        {
            var mod = Path.Combine(instance, "mods", name);
            Directory.CreateDirectory(mod);
            File.WriteAllText(Path.Combine(mod, "manifest.json"), $$"""{"name":"{{name}}","version":"1.0.0","entry":"{{name}}.dll","type":"native"}""");
            File.WriteAllText(Path.Combine(mod, name + ".dll"), "fixture");
        }
        return new MainViewModel(new LauncherData(_root));
    }

    [Fact]
    public void LanguageSwitchKeepsCustomSelectionAndFilters()
    {
        var vm = CreateViewModel();
        vm.SetLanguage(true);
        vm.Sections.SelectMany(s => s.Items).Single(i => i.Title == "Beta").IsChecked = false;
        vm.SearchText = " ALPHA ";
        vm.SelectedOnly = true;
        vm.SetLanguage(false);
        Assert.Equal(1, vm.SelectedCount);
        Assert.Equal(2, vm.TotalCount);
        Assert.Equal(" ALPHA ", vm.SearchText);
        Assert.True(vm.SelectedOnly);
        Assert.Equal("Alpha", Assert.Single(vm.Sections.SelectMany(s => s.VisibleItems)).Title);
        Assert.Equal("1 / 2 selected", vm.SelectionLabel);
    }

    [Fact]
    public async Task ExportUsesWholeSelectionWhenSearchHidesItemsAndSurvivesLanguageSwitch()
    {
        var vm = CreateViewModel();
        vm.SearchText = "Alpha";
        var zip = Path.Combine(_root, "share.zip");
        await vm.ExportAsync(zip);
        Assert.True(vm.HasExport);
        Assert.False(vm.IsBusy);
        Assert.True(vm.MessageIsSuccess);
        using (var archive = System.IO.Compression.ZipFile.OpenRead(zip))
        {
            Assert.Contains(archive.Entries, e => e.FullName.EndsWith("Alpha.dll"));
            Assert.Contains(archive.Entries, e => e.FullName.EndsWith("Beta.dll"));
        }
        vm.SetLanguage(!vm.L.Japanese);
        Assert.True(vm.HasExport);
        Assert.Equal(zip, vm.LastExport);
        Assert.True(vm.MessageIsSuccess);
        Assert.Equal(vm.L["Complete"], vm.ExportState);
    }

    [Fact]
    public void NoDataAndNoSelectionCannotExport()
    {
        var empty = new MainViewModel(new LauncherData(_root));
        Assert.False(empty.HasInstance);
        Assert.False(empty.CanExport);
        Assert.NotEmpty(empty.InstanceWarning);

        var vm = CreateViewModel();
        foreach (var section in vm.Sections) section.SetAll(false);
        vm.SelectedOnly = true;
        Assert.True(vm.NoMatches);
        Assert.False(vm.CanExport);
        vm.SelectedOnly = false;
        Assert.False(vm.NoMatches);
    }

    [Fact]
    public void InstanceWithoutContentSaysSoInsteadOfNoMatches()
    {
        var instance = Path.Combine(_root, "versions", "Empty");
        Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "version.json"),
            """{"gameVersion":"1.26.51","type":"release","packageType":"gdk","enableIsolation":true}""");
        var vm = new MainViewModel(new LauncherData(_root));
        Assert.True(vm.HasInstance);
        Assert.True(vm.NothingToShare);
        Assert.False(vm.NoMatches);
        Assert.False(vm.CanExport);
    }

    [Fact]
    public void LanguageSwitchRelabelsWithoutReloading()
    {
        var vm = CreateViewModel();
        vm.SetLanguage(false);
        var before = vm.Sections.SelectMany(s => s.Items).Select(i => i.Key).ToList();
        vm.SetLanguage(true);
        Assert.Equal(before, vm.Sections.SelectMany(s => s.Items).Select(i => i.Key).ToList());
        Assert.Equal("手動で入れた Mod", vm.Sections[1].Title);
        Assert.Contains("1.26.51", vm.ImportSteps);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
