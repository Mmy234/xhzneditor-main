using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XhznPlayer.Services;

public sealed class LocalConfig
{
    public int Display { get; set; }

    public string? ProjectDirectory { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public static LocalConfig Load()
    {
        try
        {
            if (File.Exists(AppPaths.LocalConfigFile))
            {
                var json = File.ReadAllText(AppPaths.LocalConfigFile);
                return JsonSerializer.Deserialize<LocalConfig>(json, JsonOpt) ?? new LocalConfig();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("读取 player.local.json 失败", ex);
        }
        return new LocalConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.LocalConfigFile)!);
            File.WriteAllText(AppPaths.LocalConfigFile, JsonSerializer.Serialize(this, JsonOpt));
        }
        catch (Exception ex)
        {
            Logger.Error("写入 player.local.json 失败", ex);
        }
    }

    public string ProjectDir
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ProjectDirectory) && Directory.Exists(ProjectDirectory))
                return ProjectDirectory;
            return AppPaths.DefaultProjectDir;
        }
    }

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
