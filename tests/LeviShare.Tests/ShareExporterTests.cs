using System.IO.Compression;
using System.Text.Json.Nodes;

namespace LeviShare.Tests;

public sealed class ShareExporterTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "levishare-tests-" + Guid.NewGuid().ToString("N"));
    string VersionsDir => Path.Combine(_root, "versions");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    static string PackManifest(string name, string uuid) =>
        $$"""{"format_version":2,"header":{"name":"{{name}}","uuid":"{{uuid}}","version":[1,2,0]},"modules":[{"type":"resources","uuid":"{{uuid}}-m","version":[1,2,0]}]}""";

    /// <summary>An isolated instance with packs, mods, lip packages and private data.</summary>
    Instance CreateInstance(string name = "Src")
    {
        var dir = $"versions/{name}";
        Write($"{dir}/version.json", """{"name":"Src","gameVersion":"1.21.80.3","type":"release","packageType":"gdk","enableIsolation":true}""");
        var shared = $"{dir}/Minecraft Bedrock/Users/Shared/games/com.mojang";
        Write($"{shared}/resource_packs/PackA/manifest.json", PackManifest("PackA", "pack-a"));
        Write($"{shared}/resource_packs/PackA/textures/a.png", "png");
        Write($"{shared}/resource_packs/Broken/manifest.json",
            "{\"format_version\":2,\"header\":{\"name\":\"Broken\",\"description\":\"a\nb\",\"uuid\":\"broken\",\"version\":[1,0,0]},\"modules\":[]}\n}junk");
        Write($"{shared}/behavior_packs/BpA/manifest.json", PackManifest("BpA", "bp-a"));
        Write($"{shared}/skin_packs/SkinA/manifest.json", PackManifest("SkinA", "skin-a"));
        Write($"{shared}/resource_packs/Market/manifest.json", PackManifest("Market", "market"));
        Write($"{shared}/resource_packs/Market/contents.json", "\u0001\u0002binary");
        Write($"{shared}/minecraftWorlds/w/levelname.txt", "World");
        Write($"{dir}/Minecraft Bedrock/Users/12345/games/com.mojang/resource_packs/Private/manifest.json", PackManifest("Private", "private"));

        Write($"{dir}/mods/RawMod/manifest.json", """{"name":"RawMod","entry":"RawMod.dll","version":"1.0.0","type":"native"}""");
        Write($"{dir}/mods/RawMod/RawMod.dll", "dll");
        Write($"{dir}/mods/RawMod/RawMod.pdb", "pdb");
        Write($"{dir}/mods/RawMod/config/config.json", "{}");
        Write($"{dir}/mods/RawMod/logs/latest.txt", "log");
        Write($"{dir}/mods/RawMod/data/ll_bstats_telemetry_uuid", "id");
        Write($"{dir}/mods/OffMod/manifest.json.close", """{"name":"OffMod","version":"1.0.0"}""");
        Write($"{dir}/mods/OffMod/OffMod.dll", "dll");
        Write($"{dir}/mods/LipMod/manifest.json", """{"name":"LipMod","version":"2.0.0"}""");
        Write($"{dir}/mods/LipMod/LipMod.dll", "dll");
        var lipFile = Path.Combine(_root, "versions", name, "mods", "LipMod", "LipMod.dll").Replace("\\", "\\\\");
        Write($"{dir}/tooth_lock.json",
            $$"""{"packages":[{"locked":true,"manifest":{"tooth":"github.com/Owner/LipMod","version":"2.0.0"},"files":["{{lipFile}}"]},{"locked":false,"manifest":{"tooth":"github.com/Owner/NoFiles","version":"1.0.0"},"files":[]}]}""");
        return Instance.Open(VersionsDir, name);
    }

    static Dictionary<string, ZipArchiveEntry> Entries(ZipArchive zip) => zip.Entries.ToDictionary(e => e.FullName);

    static string ReadEntry(ZipArchive zip, string name)
    {
        using var r = new StreamReader(zip.GetEntry(name)!.Open());
        return r.ReadToEnd();
    }

    [Fact]
    public void ReadsInstanceContent()
    {
        var inst = CreateInstance();
        Assert.Equal("1.21.80.3", inst.GameVersion);
        var lip = Assert.Single(inst.LipPackages);
        Assert.Equal("github.com/Owner/LipMod", lip.Identifier);
        Assert.True(lip.ExplicitInstalled);
        Assert.Equal(["LipMod"], lip.Folders);
        Assert.Equal(["OffMod", "RawMod"], inst.RawMods.Select(m => m.Folder));
        Assert.False(inst.RawMods[0].Enabled);
        Assert.Contains(inst.Packs, p => p.Key == "resource_packs/Broken" && p.Uuid == "broken");
        Assert.Equal(["resource_packs/Market"], inst.EncryptedPacks);
        Assert.DoesNotContain(inst.Packs, p => p.Name == "Private");
    }

    [Fact]
    public void ExportsOnlyShareableFilesInLeviLauncherBackupFormat()
    {
        var inst = CreateInstance();
        var plan = ShareExporter.Plan(inst, ShareSelection.Default(inst));
        var outPath = Path.Combine(_root, "share.zip");
        ShareExporter.Write(plan, outPath);

        using var zip = ZipFile.OpenRead(outPath);
        var names = Entries(zip).Keys.ToHashSet();
        string[] expected =
        [
            "backup-manifest.json",
            "gameData/Users/Shared/games/com.mojang/resource_packs/PackA/manifest.json",
            "gameData/Users/Shared/games/com.mojang/resource_packs/PackA/textures/a.png",
            "gameData/Users/Shared/games/com.mojang/behavior_packs/BpA/manifest.json",
            "mods/raw/RawMod/RawMod.dll",
            "mods/raw/RawMod/config/config.json",
        ];
        foreach (var name in expected) Assert.Contains(name, names);
        foreach (var name in names)
        {
            foreach (var banned in new[] { "12345", "minecraftWorlds", "skin_packs", "Market", "OffMod", "LipMod", ".pdb", "/logs/", "uuid" })
                Assert.DoesNotContain(banned, name);
        }

        var manifest = JsonNode.Parse(ReadEntry(zip, "backup-manifest.json"))!;
        Assert.Equal(2, manifest["formatVersion"]!.GetValue<int>());
        Assert.Equal("gdk", manifest["packageType"]!.GetValue<string>());
        Assert.Equal(["gameData", "mods"], manifest["selectedScopes"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal("safe", manifest["scopeModes"]!["gameData"]!.GetValue<string>());
        Assert.Equal(["RawMod"], manifest["rawModFolders"]!.AsArray().Select(n => n!.GetValue<string>()));
        var lip = manifest["lipPackages"]!.AsArray().Single()!;
        Assert.Equal("github.com/Owner/LipMod", lip["identifier"]!.GetValue<string>());
        Assert.Equal("2.0.0", lip["version"]!.GetValue<string>());
        Assert.True(lip["explicitInstalled"]!.GetValue<bool>());
    }

    [Fact]
    public void RepairsManifestsThatStrictJsonRejects()
    {
        var inst = CreateInstance();
        var plan = ShareExporter.Plan(inst, ShareSelection.Default(inst));
        Assert.Equal(["resource_packs/Broken"], plan.RepairedManifests);
        var outPath = Path.Combine(_root, "share.zip");
        ShareExporter.Write(plan, outPath);

        using var zip = ZipFile.OpenRead(outPath);
        var text = ReadEntry(zip, "gameData/Users/Shared/games/com.mojang/resource_packs/Broken/manifest.json");
        Assert.True(LenientJson.IsStrict(text));
        Assert.Equal("broken", JsonNode.Parse(text)!["header"]!["uuid"]!.GetValue<string>());
    }

    [Fact]
    public void RespectsSelection()
    {
        var inst = CreateInstance();
        var selection = new ShareSelection();
        selection.Packs.Add("skin_packs/SkinA");
        var plan = ShareExporter.Plan(inst, selection);
        Assert.Empty(plan.RawMods);
        Assert.Empty(plan.LipPackages);
        var outPath = Path.Combine(_root, "share.zip");
        ShareExporter.Write(plan, outPath);

        using var zip = ZipFile.OpenRead(outPath);
        Assert.Contains("gameData/Users/Shared/games/com.mojang/skin_packs/SkinA/manifest.json", Entries(zip).Keys);
        Assert.DoesNotContain(Entries(zip).Keys, k => k.StartsWith("mods/", StringComparison.Ordinal));
        var manifest = JsonNode.Parse(ReadEntry(zip, "backup-manifest.json"))!;
        Assert.Equal(["gameData"], manifest["selectedScopes"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void MeasuredItemsAddUpToThePlan()
    {
        var inst = CreateInstance();
        var stats = ShareExporter.MeasureItems(inst);
        var selection = ShareSelection.Default(inst);
        var plan = ShareExporter.Plan(inst, selection);
        var picked = selection.RawMods.Select(ShareExporter.RawModKey)
            .Concat(selection.Packs.Select(ShareExporter.PackKey))
            .Select(k => stats[k])
            .ToList();
        Assert.Equal(plan.Entries.Count, picked.Sum(s => s.Files));
        Assert.Equal(plan.TotalSize, picked.Sum(s => s.Size));
        Assert.Equal(plan.Excluded.Count, picked.Sum(s => s.Excluded));
        Assert.Equal(plan.RepairedManifests.Count, picked.Count(s => s.Repaired));
        // Disabled mods and skin packs are measured too, so ticking them later needs no re-scan.
        Assert.True(stats.ContainsKey(ShareExporter.RawModKey("OffMod")));
        Assert.True(stats.ContainsKey(ShareExporter.PackKey("skin_packs/SkinA")));
    }

    [Fact]
    public void DependencyWithoutItsParentIsInstalledExplicitly()
    {
        var dir = "versions/Deps";
        Write($"{dir}/version.json", """{"gameVersion":"1.26.51.01","type":"release","packageType":"gdk","enableIsolation":true}""");
        foreach (var mod in new[] { "LeviLamina", "Helper" }) Write($"{dir}/mods/{mod}/{mod}.dll", "dll");
        string File(string mod) => Path.Combine(_root, "versions", "Deps", "mods", mod, mod + ".dll").Replace("\\", "\\\\");
        // Same shape as lip's tooth_lock.json: dependency keys may carry a "#variant" suffix.
        Write($"{dir}/tooth_lock.json", $$$"""
            {"packages":[
              {"locked":true,"manifest":{"tooth":"github.com/LiteLDev/LeviLamina","version":"26.51.5",
                "variants":[{"label":"client","dependencies":{"github.com/LiteLDev/Helper#client":"1.*"}}]},"files":["{{{File("LeviLamina")}}}"]},
              {"locked":false,"manifest":{"tooth":"github.com/LiteLDev/Helper","version":"1.3.3","variants":[]},"files":["{{{File("Helper")}}}"]}
            ]}
            """);
        var inst = Instance.Open(VersionsDir, "Deps");
        Assert.Equal(["github.com/LiteLDev/Helper"], inst.LipPackages.Single(p => p.Identifier.EndsWith("LeviLamina")).Dependencies);

        var both = ShareExporter.Plan(inst, ShareSelection.Default(inst));
        Assert.False(both.LipPackages.Single(p => p.Identifier.EndsWith("Helper")).ExplicitInstalled);

        var onlyHelper = new ShareSelection();
        onlyHelper.LipPackages.Add("github.com/LiteLDev/Helper");
        Assert.True(Assert.Single(ShareExporter.Plan(inst, onlyHelper).LipPackages).ExplicitInstalled);
    }

    [Fact]
    public void RefusesEmptySelection()
    {
        var inst = CreateInstance();
        var plan = ShareExporter.Plan(inst, new ShareSelection());
        var ex = Assert.Throws<ShareException>(() => ShareExporter.Write(plan, Path.Combine(_root, "x.zip")));
        Assert.Equal(ShareException.NothingSelected, ex.Code);
        Assert.False(File.Exists(Path.Combine(_root, "x.zip")));
    }

    [Fact]
    public void ListsInstancesAndHonoursBaseRoot()
    {
        CreateInstance("Src");
        var data = new LauncherData(_root);
        var only = Assert.Single(data.ListInstances());
        Assert.Equal("Src", only.Name);
        Assert.Equal("1.21.80.3", only.GameVersion);
    }

    [Theory]
    [InlineData("RawMod.pdb", true)]
    [InlineData("logs/latest.txt", true)]
    [InlineData("sub/cache/x.bin", true)]
    [InlineData("data/ll_bstats_telemetry_uuid", true)]
    [InlineData("statisticsUuid.txt", true)]
    [InlineData("RawMod.dll", false)]
    [InlineData("config/config.json", false)]
    [InlineData("uuid_mapping.json", false)]
    public void SanitizerSkipsPersonalFiles(string path, bool excluded) => Assert.Equal(excluded, Sanitizer.IsExcluded(path));

    [Theory]
    [InlineData("{\"a\":1} trailing", "1")]
    [InlineData("{\"a\":\"x\ny\"}", "x y")]
    [InlineData("{\"a\":1}\n}", "1")]
    [InlineData("{/* c */ \"a\":1,}", "1")]
    public void LenientJsonAcceptsWhatTheGameAccepts(string text, string expected) =>
        Assert.Equal(expected, LenientJson.TryParse(text)!["a"]!.ToString());
}
