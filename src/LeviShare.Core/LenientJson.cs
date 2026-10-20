using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LeviShare;

/// <summary>JSON reading that accepts what Minecraft accepts.</summary>
public static class LenientJson
{
    static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonNode? TryRead(string path) => File.Exists(path) ? TryParse(File.ReadAllText(path)) : null;

    /// <summary>
    /// Parses JSON with comments and trailing commas. The game also accepts raw control characters
    /// inside strings and junk after the root object, so those are dropped on a second attempt.
    /// </summary>
    public static JsonNode? TryParse(string text)
    {
        try { return JsonNode.Parse(text, documentOptions: Options); }
        catch (JsonException) { }
        var end = text.LastIndexOf('}');
        if (end >= 0) text = text[..(end + 1)];
        var cleaned = new string(text.Select(c => c < ' ' ? ' ' : c).ToArray());
        try { return JsonNode.Parse(cleaned, documentOptions: Options); }
        catch (JsonException) { }
        // Several closing braces after the root object: keep only the first complete value.
        var cut = FirstCompleteValueEnd(cleaned);
        if (cut < 0) return null;
        try { return JsonNode.Parse(cleaned[..(cut + 1)], documentOptions: Options); }
        catch (JsonException) { return null; }
    }

    /// <summary>True when the text is strict JSON (what LeviLauncher's parser needs).</summary>
    public static bool IsStrict(string text)
    {
        try { JsonNode.Parse(text); return true; }
        catch (JsonException) { return false; }
    }

    static int FirstCompleteValueEnd(string text)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            switch (c)
            {
                case '"': inString = true; break;
                case '{' or '[': depth++; break;
                case '}' or ']':
                    depth--;
                    if (depth == 0) return i;
                    break;
            }
        }
        return -1;
    }

    public static string? String(JsonNode? node)
    {
        try { return node?.GetValue<string>(); }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException) { return node?.ToString(); }
    }

    public static bool Bool(JsonNode? node, bool fallback = false)
    {
        try { return node?.GetValue<bool>() ?? fallback; }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException) { return fallback; }
    }
}
