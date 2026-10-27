using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
    string _query = "";
    bool _selectedOnly;
    public string Title { get; } = title;
    public string Note { get; } = note;
    public ObservableCollection<ShareItem> Items { get; } = [];
    public bool IsEmpty => Items.Count == 0;
    public bool HasNote => Note.Length > 0;
    public IEnumerable<ShareItem> VisibleItems => Items.Where(i =>
        (!_selectedOnly || i.IsChecked) &&
        (i.Title.Contains(_query, StringComparison.OrdinalIgnoreCase) ||
         i.Detail.Contains(_query, StringComparison.OrdinalIgnoreCase)));
    public bool HasMatches => VisibleItems.Any();
    public string CountLabel => $"{Items.Count(i => i.IsChecked)} / {Items.Count}";

    public void Refresh(string query, bool selectedOnly = false)
    {
        if (_query != query || _selectedOnly != selectedOnly || selectedOnly)
        {
            _query = query;
            _selectedOnly = selectedOnly;
            Raise(nameof(VisibleItems));
            Raise(nameof(HasMatches));
        }
        Raise(nameof(CountLabel));
    }

    /// <summary>Ticks or unticks every row, then recalculates once instead of once per row.</summary>
    public void SetAll(bool value)
    {
        foreach (var item in VisibleItems) item.SetQuietly(value);
        changed();
    }
}

public sealed class MainViewModel : Observable
{
    Loc _l = new(Loc.SystemIsJapanese);
    LauncherData _data;
    InstanceSummary? _selected;
    Instance? _instance;
    Dictionary<string, ItemStats>? _stats;
    int _loadVersion;
    bool _hasSelection;
    string _message = "";
    string _messageClass = "note";
    string? _lastExport;
    string _searchText = "";
    bool _selectedOnly;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                Raise(nameof(HasSearch));
                RefreshSections();
            }
        }
    }
    public bool HasSearch => SearchText.Length > 0;
    public bool SelectedOnly
    {
        get => _selectedOnly;
        set { if (Set(ref _selectedOnly, value)) RefreshSections(); }
    }
    public bool HasInstance => _instance is not null;
    public bool HasItems => TotalCount > 0;
    public bool NothingToShare => HasInstance && !HasItems;
    public bool NoMatches => HasItems && !Sections.Any(s => s.HasMatches);
    public int SelectedCount => Sections.Sum(s => s.Items.Count(i => i.IsChecked));
    public int TotalCount => Sections.Sum(s => s.Items.Count);
    public string SelectionLabel => L.Format("SelectionCount", SelectedCount, TotalCount);
    public string ExportState => IsBusy ? L["ExportingShort"] : HasExport ? L["Complete"] : HasInstance && !_hasSelection ? L["Ready"] : "";
    public bool HasExportState => ExportState.Length > 0;

    void RefreshSections()
    {
        foreach (var section in Sections) section.Refresh(SearchText.Trim(), SelectedOnly);
        Raise(nameof(NoMatches));
    }

    public Loc L { get => _l; private set => Set(ref _l, value); }
    public string DataRoot => _data.Root;
    public ObservableCollection<InstanceSummary> Instances { get; } = [];
    public ObservableCollection<ShareSection> Sections { get; } = [];
    public string InstanceWarning { get; private set; } = "";
    public string Summary { get; private set; } = "";
    public string SummarySize { get; private set; } = "";
    public string Details { get; private set; } = "";
    public bool CanExport => _instance is not null && _hasSelection && !_busy;
    public bool HasExport => _lastExport is not null;
    public string? LastExport => _lastExport;
    public string ImportSteps => L.Format("ImportSteps", _instance?.GameVersion ?? "x.y.z");
    public string About => L.Format("About", ShareExporter.ToolVersion);

    public string Message { get => _message; private set => Set(ref _message, value); }

    public bool MessageIsSuccess => _messageClass == "ok";
    public bool MessageIsError => _messageClass == "bad";
    public bool MessageIsWarning => _messageClass == "warn";

    public InstanceSummary? SelectedInstance
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value)) LoadInstance();
        }
    }

    public MainViewModel() : this(LauncherData.Detect()) { }

    public MainViewModel(LauncherData data)
    {
        _data = data;
        Reload();
    }

    // Only the labels change: the loaded instance, measured sizes, selection and last export are kept,
    // so switching language never re-reads the disk.
    public void SetLanguage(bool japanese)
    {
        if (L.Japanese == japanese) return;
        var selected = Sections.SelectMany(s => s.Items)
            .Where(i => i.IsChecked).Select(i => (i.Kind, i.Key)).ToHashSet();
        L = new Loc(japanese);
        InstanceWarning = BuildWarning();
        if (_instance is { } inst)
        {
            BuildSections(inst);
            foreach (var item in Sections.SelectMany(s => s.Items))
                item.SetQuietly(selected.Contains((item.Kind, item.Key)));
        }
        Raise(nameof(InstanceWarning));
        Raise(nameof(ImportSteps));
        Raise(nameof(ExportState));
        Raise(nameof(HasExportState));
        Raise(nameof(About));
        Recalculate();
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
        _loadError = null;
        var version = ++_loadVersion;
        _lastExport = null;
        Message = "";
        if (!_data.Exists) _loadError = "NoData";
        else if (Instances.Count == 0) _loadError = "NoInstances";
        else if (_selected is { IsUwp: true }) _loadError = "UwpInstance";
        else if (_selected is not null)
        {
            try
            {
                _instance = _data.Open(_selected.Name);
            }
            catch (ShareException ex)
            {
                _loadError = ex.Code == ShareException.Uwp ? "UwpInstance" : "InstanceNotFound";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _loadError = "InstanceNotFound";
            }
        }

        InstanceWarning = BuildWarning();
        if (_instance is { } inst)
        {
            BuildSections(inst);
            _ = MeasureAsync(inst, version);
        }
        Raise(nameof(InstanceWarning));
        Raise(nameof(ImportSteps));
        Raise(nameof(HasExport));
        Raise(nameof(HasInstance));
        Recalculate();
    }

    string? _loadError;

    string BuildWarning()
    {
        if (_loadError is not null) return L[_loadError];
        if (_instance is not { } inst) return "";
        var warnings = new List<string>();
        if (!inst.EnableIsolation) warnings.Add(L["SharedData"]);
        if (inst.EncryptedPacks.Count > 0) warnings.Add(L.Format("Encrypted", string.Join(", ", inst.EncryptedPacks)));
        return string.Join("\n", warnings);
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
        Sections.Clear();
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
        SummarySize = "";
        Details = "";
        if (_instance is not null)
        {
            var lip = checkedItems.Count(i => i.Kind == ItemKind.Lip);
            var mods = checkedItems.Count(i => i.Kind == ItemKind.RawMod);
            var packs = checkedItems.Count(i => i.Kind == ItemKind.Pack);
            Summary = L.Format("Summary", lip, mods, packs);
            if (_stats is null)
            {
                SummarySize = L["Counting"];
            }
            else
            {
                var stats = checkedItems
                    .Where(i => i.Kind != ItemKind.Lip)
                    .Select(i => _stats.GetValueOrDefault(i.Kind == ItemKind.RawMod ? ShareExporter.RawModKey(i.Key) : ShareExporter.PackKey(i.Key)))
                    .OfType<ItemStats>()
                    .ToList();
                SummarySize = L.Format("SummarySize", stats.Sum(s => s.Files), FormatSize(stats.Sum(s => s.Size)));
                var details = new List<string>();
                var excluded = stats.Sum(s => s.Excluded);
                var repaired = stats.Count(s => s.Repaired);
                if (excluded > 0) details.Add(L.Format("Excluded", excluded));
                if (repaired > 0) details.Add(L.Format("Repaired", repaired));
                Details = string.Join("\n", details);
            }
        }
        Raise(nameof(Summary));
        Raise(nameof(SummarySize));
        Raise(nameof(Details));
        Raise(nameof(CanExport));
        Raise(nameof(ExportState));
        Raise(nameof(HasExportState));
        Raise(nameof(HasItems));
        Raise(nameof(NothingToShare));
        Raise(nameof(SelectedCount));
        Raise(nameof(TotalCount));
        Raise(nameof(SelectionLabel));
        RefreshSections();
    }

    public string SuggestedFileName =>
        $"{string.Concat((_instance?.Name ?? "levishare").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))}_share.zip";

    bool _busy;

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            if (Set(ref _busy, value))
            {
                Raise(nameof(CanExport));
                Raise(nameof(ExportState));
                Raise(nameof(HasExportState));
            }
        }
    }

    public async Task ExportAsync(string path)
    {
        if (IsBusy) return;
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
        catch (Exception ex)
        {
            // OnExport is async void: anything left uncaught here would close the app.
            var log = Program.WriteCrashLog(ex);
            ShowMessage(L.Format("Error", log is null ? ex.Message : $"{ex.Message} ({log})"), "bad");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ShowMessage(string text, string cssClass)
    {
        _messageClass = cssClass;
        Raise(nameof(MessageIsSuccess));
        Raise(nameof(MessageIsError));
        Raise(nameof(MessageIsWarning));
        Message = text;
    }

    static string FormatSize(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:F1} MB",
    };
}
