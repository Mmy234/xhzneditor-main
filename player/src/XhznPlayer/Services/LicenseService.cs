using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xhzn.PackFormat;

namespace XhznPlayer.Services;

public enum LicenseDecision
{
    Play,
    PlayTrialWatermark,
    Expired,
    MachineMismatch
}

public sealed class LicenseResult
{
    public LicenseDecision Decision { get; init; }
    public string Message { get; init; } = "";
}

public static class LicenseService
{
    public const string ExpiredText = "软件已到期 请联系管理员";
    public const string BindText = "本机未授权播放此项目";

    public static string GetOrCreateMachineId()
    {
        try
        {
            if (File.Exists(AppPaths.MachineFile))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.MachineFile));
                if (doc.RootElement.TryGetProperty("machineId", out var id))
                {
                    var s = id.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("读取机器指纹失败", ex);
        }

        var created = ComputeFingerprint();
        try
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            File.WriteAllText(AppPaths.MachineFile, JsonSerializer.Serialize(new { machineId = created, createdAt = DateTime.UtcNow }, JsonOpt));
        }
        catch (Exception ex)
        {
            Logger.Error("写入机器指纹失败", ex);
        }
        return created;
    }

    public static LicenseResult Evaluate(LoadedPack pack)
    {
        var machineId = GetOrCreateMachineId();
        var packId = string.IsNullOrWhiteSpace(pack.Manifest.Id) ? pack.Hash : pack.Manifest.Id;
        var bindPath = Path.Combine(AppPaths.BindingsDir, Sanitize(packId) + ".json");

        if (File.Exists(bindPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(bindPath));
                var bound = doc.RootElement.TryGetProperty("machineId", out var m) ? m.GetString() : null;
                if (!string.IsNullOrWhiteSpace(bound) &&
                    !string.Equals(bound, machineId, StringComparison.Ordinal))
                {
                    Logger.Warn($"绑机失败 pack={packId}");
                    return new LicenseResult { Decision = LicenseDecision.MachineMismatch, Message = BindText };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("读取绑机信息失败", ex);
            }
        }

        var deadline = ResolveDeadline(pack, packId);
        if (deadline != null && DateTime.Today >= deadline.Value.Date)
            return new LicenseResult { Decision = LicenseDecision.Expired, Message = ExpiredText };

        var trialWatermark = pack.LicenseSectionMissing;
        return new LicenseResult
        {
            Decision = trialWatermark ? LicenseDecision.PlayTrialWatermark : LicenseDecision.Play
        };
    }

    public static void BindAfterLoadStart(LoadedPack pack)
    {
        var machineId = GetOrCreateMachineId();
        var packId = string.IsNullOrWhiteSpace(pack.Manifest.Id) ? pack.Hash : pack.Manifest.Id;
        var bindPath = Path.Combine(AppPaths.BindingsDir, Sanitize(packId) + ".json");
        if (File.Exists(bindPath))
            return;
        try
        {
            Directory.CreateDirectory(AppPaths.BindingsDir);
            File.WriteAllText(bindPath, JsonSerializer.Serialize(new
            {
                packId,
                machineId,
                boundAt = DateTime.UtcNow
            }, JsonOpt));
        }
        catch (Exception ex)
        {
            Logger.Error("写入绑机信息失败", ex);
        }
    }

    private static DateTime? ResolveDeadline(LoadedPack pack, string packId)
    {
        DateTime? expire = null;
        DateTime? trialEnd = null;
        var license = pack.Manifest.License;

        if (license != null && !string.IsNullOrWhiteSpace(license.ExpireDate)
            && DateTime.TryParseExact(license.ExpireDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d))
        {
            expire = d.Date;
        }

        var trialDays = license?.TrialDays;
        if (trialDays is > 0)
        {
            var first = GetOrCreateFirstOpen(packId);
            trialEnd = first.Date.AddDays(trialDays.Value);
        }

        if (expire == null) return trialEnd;
        if (trialEnd == null) return expire;
        return expire < trialEnd ? expire : trialEnd;
    }

    private static DateTime GetOrCreateFirstOpen(string packId)
    {
        var path = Path.Combine(AppPaths.TrialsDir, Sanitize(packId) + ".json");
        try
        {
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("firstOpenUtc", out var v)
                    && DateTime.TryParse(v.GetString(), out var stored))
                    return stored.ToLocalTime();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("读取试用起始日失败", ex);
        }

        var now = DateTime.UtcNow;
        try
        {
            Directory.CreateDirectory(AppPaths.TrialsDir);
            File.WriteAllText(path, JsonSerializer.Serialize(new { firstOpenUtc = now }, JsonOpt));
        }
        catch (Exception ex)
        {
            Logger.Error("写入试用起始日失败", ex);
        }
        return now.ToLocalTime();
    }

    private static string ComputeFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(Environment.MachineName).Append('|');
        try
        {
            var guid = Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "") as string;
            sb.Append(guid);
        }
        catch { /* ignore */ }
        sb.Append('|').Append(Environment.OSVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    private static string Sanitize(string id)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            id = id.Replace(c, '_');
        return string.IsNullOrWhiteSpace(id) ? "unknown" : id;
    }

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
