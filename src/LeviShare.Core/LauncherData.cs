namespace LeviShare;

/// <summary>Where LeviLauncher keeps its instances.</summary>
public sealed class LauncherData
{
    public string Root { get; }
    public string VersionsDir => Path.Combine(Root, "versions");

    public LauncherData(string root) => Root = Path.GetFullPath(root);

    /// <summary>
    /// LeviLauncher stores its config in %APPDATA%\levilauncher.exe; "base_root" there moves the
    /// instances elsewhere.
    /// </summary>
    public static string DefaultRoot()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "levilauncher.exe");
        var baseRoot = LenientJson.String(LenientJson.TryRead(Path.Combine(root, "config.json"))?["base_root"]);
        return string.IsNullOrWhiteSpace(baseRoot) ? root : baseRoot;
    }

    public static LauncherData Detect() => new(DefaultRoot());

    public bool Exists => Directory.Exists(VersionsDir);

    public IReadOnlyList<InstanceSummary> ListInstances()
    {
        if (!Exists) return [];
        var result = new List<InstanceSummary>();
        foreach (var dir in Directory.GetDirectories(VersionsDir).Order(StringComparer.OrdinalIgnoreCase))
        {
            var meta = LenientJson.TryRead(Path.Combine(dir, "version.json"));
            if (meta is null) continue;
            result.Add(new InstanceSummary(
                Path.GetFileName(dir),
                LenientJson.String(meta["gameVersion"]) ?? "",
                (LenientJson.String(meta["type"]) ?? "release").ToLowerInvariant(),
                (LenientJson.String(meta["packageType"]) ?? "gdk").ToLowerInvariant()));
        }
        return result;
    }

    public Instance Open(string name) => Instance.Open(VersionsDir, name);
}

public sealed record InstanceSummary(string Name, string GameVersion, string Type, string PackageType)
{
    public bool IsUwp => PackageType == "uwp";

    /// <summary>What to show next to the name: the game version when the instance was renamed, plus Preview / UWP.</summary>
    public string Note => string.Join(" · ", new[]
    {
        GameVersion != Name ? GameVersion : "",
        Type.Equals("preview", StringComparison.OrdinalIgnoreCase) ? "Preview" : "",
        IsUwp ? "UWP" : "",
    }.Where(s => s.Length > 0));
}
