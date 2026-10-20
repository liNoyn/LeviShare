using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace LeviShare;

/// <summary>What the user picked to share. Keys: lip identifiers, raw mod folders, <see cref="Pack.Key"/>.</summary>
public sealed class ShareSelection
{
    public HashSet<string> LipPackages { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> RawMods { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Packs { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Everything except disabled mods and skin packs.</summary>
    public static ShareSelection Default(Instance instance)
    {
        var s = new ShareSelection();
        foreach (var p in instance.LipPackages) s.LipPackages.Add(p.Identifier);
        foreach (var m in instance.RawMods.Where(m => m.Enabled)) s.RawMods.Add(m.Folder);
        foreach (var p in instance.Packs.Where(p => p.Root != "skin_packs")) s.Packs.Add(p.Key);
        return s;
    }
}

/// <summary>What one raw mod or pack adds to an export.</summary>
public sealed record ItemStats(int Files, long Size, int Excluded, bool Repaired);

/// <summary>One file in the archive. <see cref="Content"/> replaces the file (repaired manifests).</summary>
public sealed record ShareEntry(string ArchivePath, string SourcePath, long Size, string? Content = null);

/// <summary>The files an export would write, computed before writing so the app can show a summary.</summary>
public sealed class SharePlan
{
    public required Instance Instance { get; init; }
    public required IReadOnlyList<LipPackage> LipPackages { get; init; }
    public required IReadOnlyList<RawMod> RawMods { get; init; }
    public required IReadOnlyList<Pack> Packs { get; init; }
    public List<ShareEntry> Entries { get; } = [];
    public List<string> Excluded { get; } = [];
    public List<string> RepairedManifests { get; } = [];

    public long TotalSize => Entries.Sum(e => e.Size);
    public bool IsEmpty => LipPackages.Count == 0 && RawMods.Count == 0 && Packs.Count == 0;
}

/// <summary>
/// Writes a share archive in LeviLauncher's instance backup format (formatVersion 2), so the
/// recipient imports it with LeviLauncher's own "Restore backup" (no LeviShare needed).
/// See internal/mcservice/instance_backup.go in LiteLDev/LeviLauncher.
/// </summary>
public static class ShareExporter
{
    public const string ManifestName = "backup-manifest.json";
    public const string InfoName = "levishare-info.json";
    const string SharedMojangInArchive = "gameData/Users/Shared/games/com.mojang";

    // Same list and order as instanceBackupSafeBedrockRoots in LeviLauncher.
    static readonly string[] BedrockWhitelistRoots =
    [
        "behavior_packs", "development_behavior_packs", "development_resource_packs", "development_skin_packs",
        "resource_packs", "minecraftWorlds", "custom_skins", "Screenshots", "skin_packs", "world_templates", "config",
    ];

    public static string ToolVersion =>
        typeof(ShareExporter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public static SharePlan Plan(Instance instance, ShareSelection selection)
    {
        var plan = new SharePlan
        {
            Instance = instance,
            LipPackages = instance.LipPackages.Where(p => selection.LipPackages.Contains(p.Identifier)).ToList(),
            RawMods = instance.RawMods.Where(m => selection.RawMods.Contains(m.Folder)).ToList(),
            Packs = instance.Packs.Where(p => selection.Packs.Contains(p.Key)).ToList(),
        };
        foreach (var mod in plan.RawMods)
            AddDir(plan, mod.Path, $"mods/raw/{mod.Folder}", sanitize: true);
        foreach (var pack in plan.Packs)
        {
            var prefix = $"{SharedMojangInArchive}/{pack.Root}/{pack.Folder}";
            AddDir(plan, pack.Path, prefix, sanitize: false);
            RepairManifest(plan, pack, $"{prefix}/manifest.json");
        }
        return plan;
    }

    /// <summary>
    /// Files, size and exclusions of every raw mod and pack, measured once. Keys are
    /// <see cref="RawModKey"/> and <see cref="PackKey"/>. Summing these for the selected items is
    /// cheap, unlike planning again (a large setup has tens of thousands of files).
    /// </summary>
    public static Dictionary<string, ItemStats> MeasureItems(Instance instance)
    {
        var all = new ShareSelection();
        foreach (var m in instance.RawMods) all.RawMods.Add(m.Folder);
        foreach (var p in instance.Packs) all.Packs.Add(p.Key);
        var plan = Plan(instance, all);
        var repaired = plan.RepairedManifests.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stats = new Dictionary<string, ItemStats>(StringComparer.OrdinalIgnoreCase);
        void Measure(string key, string prefix, bool isRepaired)
        {
            prefix += "/";
            var entries = plan.Entries.Where(e => e.ArchivePath.StartsWith(prefix, StringComparison.Ordinal)).ToList();
            stats[key] = new ItemStats(entries.Count, entries.Sum(e => e.Size),
                plan.Excluded.Count(x => x.StartsWith(prefix, StringComparison.Ordinal)), isRepaired);
        }
        foreach (var m in plan.RawMods) Measure(RawModKey(m.Folder), $"mods/raw/{m.Folder}", false);
        foreach (var p in plan.Packs) Measure(PackKey(p.Key), $"{SharedMojangInArchive}/{p.Root}/{p.Folder}", repaired.Contains(p.Key));
        return stats;
    }

    public static string RawModKey(string folder) => "mod:" + folder;
    public static string PackKey(string packKey) => "pack:" + packKey;

    static void AddDir(SharePlan plan, string dir, string prefix, bool sanitize)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            if (sanitize && Sanitizer.IsExcluded(rel))
            {
                plan.Excluded.Add($"{prefix}/{rel}");
                continue;
            }
            plan.Entries.Add(new ShareEntry($"{prefix}/{rel}", file, new FileInfo(file).Length));
        }
    }

    // Manifests the game accepts but strict JSON rejects make LeviLauncher fall back to one conflict
    // per file instead of one per pack, so they are re-serialized (same content) in the archive.
    static void RepairManifest(SharePlan plan, Pack pack, string archivePath)
    {
        var i = plan.Entries.FindIndex(e => e.ArchivePath == archivePath);
        if (i < 0) return;
        var text = File.ReadAllText(plan.Entries[i].SourcePath);
        if (LenientJson.IsStrict(text)) return;
        if (LenientJson.TryParse(text) is not { } node) return;
        var fixedText = node.ToJsonString(LenientJson.Indented);
        plan.Entries[i] = plan.Entries[i] with { Content = fixedText, Size = Encoding.UTF8.GetByteCount(fixedText) };
        plan.RepairedManifests.Add(pack.Key);
    }

    public static void Write(SharePlan plan, string outPath)
    {
        if (plan.IsEmpty) throw new ShareException(ShareException.NothingSelected, "Nothing is selected.");
        var scopes = new List<string>();
        if (plan.Packs.Count > 0) scopes.Add("gameData");
        if (plan.RawMods.Count > 0 || plan.LipPackages.Count > 0) scopes.Add("mods");

        var tmp = outPath + ".partial";
        try
        {
            using (var fs = File.Create(tmp))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                WriteText(zip, ManifestName, BuildManifest(plan, scopes).ToJsonString(LenientJson.Indented));
                WriteText(zip, InfoName, BuildInfo(plan).ToJsonString(LenientJson.Indented));
                if (scopes.Contains("gameData")) zip.CreateEntry("gameData/");
                if (scopes.Contains("mods"))
                {
                    zip.CreateEntry("mods/");
                    zip.CreateEntry("mods/raw/");
                }
                foreach (var e in plan.Entries)
                {
                    if (e.Content is not null) WriteText(zip, e.ArchivePath, e.Content);
                    else zip.CreateEntryFromFile(e.SourcePath, e.ArchivePath, CompressionLevel.Optimal);
                }
            }
            File.Move(tmp, outPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    static JsonObject BuildManifest(SharePlan plan, List<string> scopes) => new()
    {
        ["packageType"] = "gdk",
        ["formatVersion"] = 2,
        ["purpose"] = "share",
        ["name"] = plan.Instance.Name,
        ["gameVersion"] = plan.Instance.GameVersion,
        ["type"] = plan.Instance.Type,
        ["enableIsolation"] = plan.Instance.EnableIsolation,
        ["createdAt"] = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
        ["selectedScopes"] = Array(scopes),
        ["scopeModes"] = plan.Packs.Count > 0 ? new JsonObject { ["gameData"] = "safe" } : new JsonObject(),
        ["bedrockWhitelistRoots"] = Array(BedrockWhitelistRoots),
        ["lipPackages"] = new JsonArray(plan.LipPackages.Select(p => (JsonNode)new JsonObject
        {
            ["identifier"] = p.Identifier,
            ["version"] = p.Version,
            ["explicitInstalled"] = p.ExplicitInstalled,
            ["folders"] = Array(p.Folders),
        }).ToArray()),
        ["rawModFolders"] = Array(plan.RawMods.Select(m => m.Folder)),
    };

    // Not read by LeviLauncher: lets the recipient see what is inside and check the DLLs.
    static JsonObject BuildInfo(SharePlan plan) => new()
    {
        ["tool"] = $"LeviShare {ToolVersion}",
        ["gameVersion"] = plan.Instance.GameVersion,
        ["lipPackages"] = Array(plan.LipPackages.Select(p => $"{p.Identifier}@{p.Version}")),
        ["mods"] = new JsonArray(plan.RawMods.Select(m => (JsonNode)new JsonObject
        {
            ["folder"] = m.Folder,
            ["name"] = m.Name,
            ["version"] = m.Version,
            ["dllSha256"] = new JsonObject(plan.Entries
                .Where(e => e.ArchivePath.StartsWith($"mods/raw/{m.Folder}/", StringComparison.Ordinal)
                            && e.ArchivePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(e => KeyValuePair.Create(e.ArchivePath[$"mods/raw/{m.Folder}/".Length..], (JsonNode?)Sha256(e.SourcePath)))),
        }).ToArray()),
        ["packs"] = new JsonArray(plan.Packs.Select(p => (JsonNode)new JsonObject
        {
            ["type"] = p.Root,
            ["name"] = p.Name,
            ["version"] = p.Version,
            ["uuid"] = p.Uuid,
        }).ToArray()),
    };

    static JsonArray Array(IEnumerable<string> values) => new(values.Select(v => (JsonNode)v).ToArray());

    static void WriteText(ZipArchive zip, string name, string text)
    {
        using var w = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        w.Write(text);
    }

    static string Sha256(string path)
    {
        using var s = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }
}
