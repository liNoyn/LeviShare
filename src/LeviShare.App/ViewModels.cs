using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace LeviShare.App;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum ItemKind { Lip, RawMod, Pack }

/// <summary>One checkbox row: a lip package, a mod folder or a pack.</summary>
public sealed class ShareItem(ItemKind kind, string key, string title, string detail, bool isChecked, Action changed) : Observable
{
    bool _isChecked = isChecked;

    public ItemKind Kind { get; } = kind;
    public string Key { get; } = key;
    public string Title { get; } = title;
    public string Detail { get; } = detail;

    public bool IsChecked
    {
        get => _isChecked;
        set { if (Set(ref _isChecked, value)) changed(); }
    }

    /// <summary>Changes the tick without the change callback (for bulk changes).</summary>
    public void SetQuietly(bool value) => Set(ref _isChecked, value, nameof(IsChecked));
}

public sealed class ShareSection(string title, string note, Action changed) : Observable
{
    public string Title { get; } = title;
    public string Note { get; } = note;
    public ObservableCollection<ShareItem> Items { get; } = [];
    public bool IsEmpty => Items.Count == 0;
    public bool HasNote => Note.Length > 0;

    /// <summary>Ticks or unticks every row, then recalculates once instead of once per row.</summary>
    public void SetAll(bool value)
    {
        foreach (var item in Items) item.SetQuietly(value);
        changed();
    }
}

public sealed class MainViewModel : Observable
{
    Loc _l = new(Loc.SystemIsJapanese);
    LauncherData _data = LauncherData.Detect();
    InstanceSummary? _selected;
    Instance? _instance;
    Dictionary<string, ItemStats>? _stats;
    int _loadVersion;
    bool _hasSelection;
    string _message = "";
    string _messageClass = "note";
    string? _lastExport;

    public Loc L { get => _l; private set => Set(ref _l, value); }
    public string DataRoot => _data.Root;
    public ObservableCollection<InstanceSummary> Instances { get; } = [];
    public ObservableCollection<ShareSection> Sections { get; } = [];
    public string InstanceWarning { get; private set; } = "";
    public string Summary { get; private set; } = "";
    public string Details { get; private set; } = "";
    public bool CanExport => _instance is not null && _hasSelection && !_busy;
    public bool HasExport => _lastExport is not null;
    public string? LastExport => _lastExport;
    public string ImportSteps => L.Format("ImportSteps", _instance?.GameVersion ?? "x.y.z");
    public string About => L.Format("About", ShareExporter.ToolVersion);

    public string Message { get => _message; private set => Set(ref _message, value); }

    public IBrush MessageBrush => _messageClass switch
    {
        "ok" => new SolidColorBrush(Color.Parse("#7FD8A8")),
        "bad" => new SolidColorBrush(Color.Parse("#FF7B6B")),
        "warn" => new SolidColorBrush(Color.Parse("#F0A050")),
        _ => new SolidColorBrush(Color.Parse("#C8C8C8")),
    };

    public InstanceSummary? SelectedInstance
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value)) LoadInstance();
        }
    }

    public MainViewModel() => Reload();

    public void SetLanguage(bool japanese)
    {
        L = new Loc(japanese);
        LoadInstance();
        Raise(nameof(About));
    }

    public void SetDataRoot(string root)
    {
        // Accept either the data folder or its "versions" folder.
        if (Path.GetFileName(root.TrimEnd('\\', '/')).Equals("versions", StringComparison.OrdinalIgnoreCase))
            root = Path.GetDirectoryName(root.TrimEnd('\\', '/'))!;
        _data = new LauncherData(root);
        Raise(nameof(DataRoot));
        Reload();
    }

    public void Reload()
    {
        var previous = _selected?.Name;
        Instances.Clear();
        foreach (var i in _data.ListInstances()) Instances.Add(i);
        _selected = null;
        SelectedInstance = Instances.FirstOrDefault(i => i.Name == previous) ?? Instances.FirstOrDefault(i => !i.IsUwp) ?? Instances.FirstOrDefault();
        if (Instances.Count == 0) LoadInstance();
    }

    void LoadInstance()
    {
        Sections.Clear();
        _instance = null;
        _stats = null;
        var version = ++_loadVersion;
        InstanceWarning = "";
        _lastExport = null;
        Message = "";
        if (!_data.Exists) InstanceWarning = L["NoData"];
        else if (Instances.Count == 0) InstanceWarning = L["NoInstances"];
        else if (_selected is { IsUwp: true }) InstanceWarning = L["UwpInstance"];
        else if (_selected is not null)
        {
            try
            {
                _instance = _data.Open(_selected.Name);
            }
            catch (ShareException ex)
            {
                InstanceWarning = ex.Code == ShareException.Uwp ? L["UwpInstance"] : L["InstanceNotFound"];
            }
        }

        if (_instance is { } inst)
        {
            var warnings = new List<string>();
            if (!inst.EnableIsolation) warnings.Add(L["SharedData"]);
            if (inst.EncryptedPacks.Count > 0) warnings.Add(L.Format("Encrypted", string.Join(", ", inst.EncryptedPacks)));
            InstanceWarning = string.Join("\n", warnings);
            BuildSections(inst);
            _ = MeasureAsync(inst, version);
        }
        Raise(nameof(InstanceWarning));
        Raise(nameof(ImportSteps));
        Raise(nameof(HasExport));
        Recalculate();
    }

    // Counting tens of thousands of files takes seconds, so it runs off the UI thread once per
    // instance. A result for an instance that is no longer selected is dropped.
    async Task MeasureAsync(Instance inst, int version)
    {
        try
        {
            var stats = await Task.Run(() => ShareExporter.MeasureItems(inst));
            if (version != _loadVersion) return;
            _stats = stats;
            Recalculate();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (version == _loadVersion) ShowMessage(L.Format("Error", ex.Message), "bad");
        }
    }

    void BuildSections(Instance inst)
    {
        var defaults = ShareSelection.Default(inst);

        var lip = new ShareSection(L["LipTitle"], L["LipNote"], Recalculate);
        foreach (var p in inst.LipPackages)
            lip.Items.Add(new ShareItem(ItemKind.Lip, p.Identifier, p.Identifier,
                p.ExplicitInstalled ? p.Version : $"{p.Version} · {L["Dependency"]}",
                defaults.LipPackages.Contains(p.Identifier), Recalculate));

        var raw = new ShareSection(L["RawTitle"], L["RawNote"], Recalculate);
        foreach (var m in inst.RawMods)
        {
            var detail = string.Join(" · ", new[] { m.Version, m.Name != m.Folder ? m.Folder : "", m.Enabled ? "" : L["Disabled"] }.Where(s => s.Length > 0));
            raw.Items.Add(new ShareItem(ItemKind.RawMod, m.Folder, m.Name, detail, defaults.RawMods.Contains(m.Folder), Recalculate));
        }

        Sections.Add(lip);
        Sections.Add(raw);
        foreach (var (root, title) in new[] { ("resource_packs", "ResourceTitle"), ("behavior_packs", "BehaviorTitle"), ("skin_packs", "SkinTitle") })
        {
            var section = new ShareSection(L[title], "", Recalculate);
            foreach (var p in inst.Packs.Where(p => p.Root == root))
                section.Items.Add(new ShareItem(ItemKind.Pack, p.Key, p.Name, $"{p.Version} · {p.Folder}", defaults.Packs.Contains(p.Key), Recalculate));
            Sections.Add(section);
        }
    }

    ShareSelection CurrentSelection()
    {
        var s = new ShareSelection();
        foreach (var item in Sections.SelectMany(x => x.Items).Where(i => i.IsChecked))
        {
            var set = item.Kind switch
            {
                ItemKind.Lip => s.LipPackages,
                ItemKind.RawMod => s.RawMods,
                _ => s.Packs,
            };
            set.Add(item.Key);
        }
        return s;
    }

    // Only adds up numbers measured in advance, so ticking boxes never touches the disk.
    void Recalculate()
    {
        var checkedItems = Sections.SelectMany(s => s.Items).Where(i => i.IsChecked).ToList();
        _hasSelection = checkedItems.Count > 0;
        Summary = "";
        Details = "";
        if (_instance is not null)
        {
            var lip = checkedItems.Count(i => i.Kind == ItemKind.Lip);
            var mods = checkedItems.Count(i => i.Kind == ItemKind.RawMod);
            var packs = checkedItems.Count(i => i.Kind == ItemKind.Pack);
            if (_stats is null)
            {
                Summary = L.Format("SummaryCounting", lip, mods, packs);
            }
            else
            {
                var stats = checkedItems
                    .Where(i => i.Kind != ItemKind.Lip)
                    .Select(i => _stats.GetValueOrDefault(i.Kind == ItemKind.RawMod ? ShareExporter.RawModKey(i.Key) : ShareExporter.PackKey(i.Key)))
                    .OfType<ItemStats>()
                    .ToList();
                Summary = L.Format("Summary", lip, mods, packs, stats.Sum(s => s.Files), FormatSize(stats.Sum(s => s.Size)));
                var details = new List<string>();
                var excluded = stats.Sum(s => s.Excluded);
                var repaired = stats.Count(s => s.Repaired);
                if (excluded > 0) details.Add(L.Format("Excluded", excluded));
                if (repaired > 0) details.Add(L.Format("Repaired", repaired));
                Details = string.Join("  ·  ", details);
            }
        }
        Raise(nameof(Summary));
        Raise(nameof(Details));
        Raise(nameof(CanExport));
    }

    public string SuggestedFileName =>
        $"{string.Concat((_instance?.Name ?? "levishare").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))}_share.zip";

    bool _busy;

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value)) Raise(nameof(CanExport));
        }
    }

    public async Task ExportAsync(string path)
    {
        if (_instance is not { } instance || !_hasSelection)
        {
            ShowMessage(L["NothingSelected"], "warn");
            return;
        }
        IsBusy = true;
        ShowMessage(L["Exporting"], "note");
        try
        {
            // Large setups take tens of seconds, so plan and write off the UI thread. Planning here
            // picks up files changed since the instance was loaded.
            var selection = CurrentSelection();
            await Task.Run(() => ShareExporter.Write(ShareExporter.Plan(instance, selection), path));
            _lastExport = path;
            Raise(nameof(HasExport));
            ShowMessage(L.Format("Exported", path), "ok");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ShareException)
        {
            ShowMessage(L.Format("Error", ex.Message), "bad");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ShowMessage(string text, string cssClass)
    {
        _messageClass = cssClass;
        Raise(nameof(MessageBrush));
        Message = text;
    }

    static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:F1} MB",
    };
}
