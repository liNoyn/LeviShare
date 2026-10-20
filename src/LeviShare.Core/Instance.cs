using System.Text.Json.Nodes;

namespace LeviShare;

/// <summary>A mod installed with lip. Only its id and version are shared; LeviLauncher reinstalls it.</summary>
public sealed record LipPackage(string Identifier, string Version, bool ExplicitInstalled, IReadOnlyList<string> Folders);

/// <summary>A mod folder copied into mods/ by hand. Shared as files.</summary>
public sealed record RawMod(string Folder, string Name, string Version, bool Enabled, string Path);

/// <summary>A resource, behavior or skin pack in the shared com.mojang folder.</summary>
public sealed record Pack(string Root, string Folder, string Name, string Version, string Uuid, string Path)
{
    public string Key => $"{Root}/{Folder}";
}

/// <summary>What a LeviLauncher instance contains that can be shared.</summary>
public sealed class Instance
{
    public static readonly string[] PackRoots = ["resource_packs", "behavior_packs", "skin_packs"];

    public required string Name { get; init; }
    public required string Dir { get; init; }
    public required string GameVersion { get; init; }
    public required string Type { get; init; }
    public required bool EnableIsolation { get; init; }
    public required string GameDataRoot { get; init; }
    public List<LipPackage> LipPackages { get; } = [];
    public List<RawMod> RawMods { get; } = [];
    public List<Pack> Packs { get; } = [];

    /// <summary>Marketplace packs (encrypted); they may not be redistributed and are never shared.</summary>
    public List<string> EncryptedPacks { get; } = [];

    public string ModsDir => System.IO.Path.Combine(Dir, "mods");

    /// <summary>
    /// Only Users/Shared is shared: per-user folders are named after the player's account and differ
    /// between PCs.
    /// </summary>
    public string SharedMojang => System.IO.Path.Combine(GameDataRoot, "Users", "Shared", "games", "com.mojang");

    public static Instance Open(string versionsDir, string name, string? appDataOverride = null)
    {
        var dir = System.IO.Path.Combine(versionsDir, name);
        var meta = LenientJson.TryRead(System.IO.Path.Combine(dir, "version.json"))
            ?? throw new ShareException(ShareException.InstanceNotFound, $"Instance not found: {name}");

        var type = (LenientJson.String(meta["type"]) ?? "release").ToLowerInvariant();
        var packageType = (LenientJson.String(meta["packageType"]) ?? "gdk").ToLowerInvariant();
        if (packageType == "uwp") throw new ShareException(ShareException.Uwp, "UWP instances cannot load mods and are not supported.");
        var isolation = LenientJson.Bool(meta["enableIsolation"]);
        var gameDir = type == "preview" ? "Minecraft Bedrock Preview" : "Minecraft Bedrock";
        var appData = appDataOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var instance = new Instance
        {
            Name = name,
            Dir = dir,
            GameVersion = LenientJson.String(meta["gameVersion"]) ?? "",
            Type = type,
            EnableIsolation = isolation,
            GameDataRoot = isolation ? System.IO.Path.Combine(dir, gameDir) : System.IO.Path.Combine(appData, gameDir),
        };
        instance.LoadLipPackages();
        instance.LoadRawMods();
        instance.LoadPacks();
        return instance;
    }

    // lip's lock file lists every installed package with the files it owns. Like LeviLauncher's own
    // backup, only packages that own a folder under mods/ are recorded.
    void LoadLipPackages()
    {
        var lockJson = LenientJson.TryRead(System.IO.Path.Combine(Dir, "tooth_lock.json"));
        if (lockJson?["packages"] is not JsonArray packages) return;
        var modsPrefix = System.IO.Path.GetFullPath(ModsDir).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
        foreach (var p in packages)
        {
            var tooth = LenientJson.String(p?["manifest"]?["tooth"]);
            var version = LenientJson.String(p?["manifest"]?["version"]);
            if (string.IsNullOrWhiteSpace(tooth) || string.IsNullOrWhiteSpace(version)) continue;
            var folders = (p!["files"] as JsonArray ?? [])
                .Select(f => LenientJson.String(f) ?? "")
                .Select(f => f.Replace('/', System.IO.Path.DirectorySeparatorChar))
                .Where(f => f.StartsWith(modsPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(f => f[modsPrefix.Length..].Split(System.IO.Path.DirectorySeparatorChar)[0])
                .Where(f => Directory.Exists(System.IO.Path.Combine(ModsDir, f)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (folders.Count == 0) continue;
            // lip locks packages the user installed on purpose; dependencies are not locked.
            LipPackages.Add(new LipPackage(tooth, version, LenientJson.Bool(p["locked"]), folders));
        }
        LipPackages.Sort((a, b) => string.Compare(a.Identifier, b.Identifier, StringComparison.OrdinalIgnoreCase));
    }

    void LoadRawMods()
    {
        if (!Directory.Exists(ModsDir)) return;
        var lipFolders = LipPackages.SelectMany(p => p.Folders).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in Directory.GetDirectories(ModsDir).Order(StringComparer.OrdinalIgnoreCase))
        {
            var folder = System.IO.Path.GetFileName(dir);
            if (lipFolders.Contains(folder)) continue;
            // LeviLauncher disables a mod by renaming manifest.json to manifest.json.close.
            var enabled = File.Exists(System.IO.Path.Combine(dir, "manifest.json"));
            var manifestPath = System.IO.Path.Combine(dir, enabled ? "manifest.json" : "manifest.json.close");
            if (!File.Exists(manifestPath)) continue;
            var manifest = LenientJson.TryRead(manifestPath);
            RawMods.Add(new RawMod(
                folder,
                LenientJson.String(manifest?["name"]) ?? folder,
                LenientJson.String(manifest?["version"]) ?? "",
                enabled,
                dir));
        }
    }

    void LoadPacks()
    {
        foreach (var root in PackRoots)
        {
            var rootDir = System.IO.Path.Combine(SharedMojang, root);
            if (!Directory.Exists(rootDir)) continue;
            foreach (var dir in Directory.GetDirectories(rootDir).Order(StringComparer.OrdinalIgnoreCase))
            {
                var folder = System.IO.Path.GetFileName(dir);
                var contents = System.IO.Path.Combine(dir, "contents.json");
                if (File.Exists(contents) && LenientJson.TryRead(contents) is null)
                {
                    EncryptedPacks.Add($"{root}/{folder}");
                    continue;
                }
                var manifest = LenientJson.TryRead(System.IO.Path.Combine(dir, "manifest.json"))
                    ?? LenientJson.TryRead(System.IO.Path.Combine(dir, "pack_manifest.json"));
                if (manifest is null) continue;
                var header = manifest["header"];
                var version = header?["version"] switch
                {
                    JsonArray a => string.Join('.', a.Select(x => x?.ToString())),
                    JsonNode n => n.ToString(),
                    _ => "",
                };
                Packs.Add(new Pack(root, folder, LenientJson.String(header?["name"]) ?? folder, version,
                    LenientJson.String(header?["uuid"]) ?? "", dir));
            }
        }
    }
}

/// <summary>An expected failure. <see cref="Code"/> lets the app show a translated message.</summary>
public sealed class ShareException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;

    public const string InstanceNotFound = "instance_not_found";
    public const string Uwp = "uwp";
    public const string NothingSelected = "nothing_selected";
}
