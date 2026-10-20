namespace LeviShare;

/// <summary>Files inside a mod folder that are personal or useless to others.</summary>
public static class Sanitizer
{
    static readonly HashSet<string> ExcludedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "logs", "cache", "temp", "tmp", "crash", "crashes", "replays", "projects", "screenshots",
    };

    static readonly string[] ExcludedExtensions = [".pdb", ".log", ".bak", ".tmp", ".dmp"];

    /// <param name="relativePath">Path inside the mod folder, with / or \ separators.</param>
    public static bool IsExcluded(string relativePath)
    {
        var parts = relativePath.Split('/', '\\');
        if (parts[..^1].Any(ExcludedDirs.Contains)) return true;
        var file = parts[^1];
        if (ExcludedExtensions.Any(e => file.EndsWith(e, StringComparison.OrdinalIgnoreCase))) return true;
        // Per-install identifiers such as ll_bstats_telemetry_uuid or statisticsUuid.
        var stem = Path.GetFileNameWithoutExtension(file);
        if (stem.Contains("uuid", StringComparison.OrdinalIgnoreCase)
            && (!Path.HasExtension(file) || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }
}
