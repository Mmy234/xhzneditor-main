using System.Text.Json;
using System.Text.Json.Serialization;

namespace Xhzn.PackFormat;

public sealed class Manifest
{
    public int SchemaVersion { get; set; }
    public int PublishVersion { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DesignInfo? Design { get; set; }
    public List<string> Pages { get; set; } = [];
    public string? EntryPageId { get; set; }
    public LicenseInfo? License { get; set; }
    public PlayerBehavior? Player { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class DesignInfo
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
}

public sealed class LicenseInfo
{
    public string? ExpireDate { get; set; }
    public int? TrialDays { get; set; }
}

public sealed class PlayerBehavior
{
    public bool AutoStart { get; set; }
    public bool CursorVisible { get; set; }
    public string? ExitPassword { get; set; }
}

public static class PackJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static Manifest ParseManifest(string json)
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(json, Options)
                       ?? throw new InvalidDataException("manifest.json 为空。");
        return manifest;
    }

    public static bool LicenseSectionMissing(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return !doc.RootElement.TryGetProperty("license", out _);
    }
}
