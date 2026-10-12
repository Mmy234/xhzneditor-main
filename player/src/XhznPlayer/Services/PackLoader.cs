using System.IO;
using System.Security.Cryptography;
using Xhzn.PackFormat;

namespace XhznPlayer.Services;

public sealed class LoadedPack
{
    public required string PackPath { get; init; }
    public required string ExtractDir { get; init; }
    public required string Hash { get; init; }
    public required Manifest Manifest { get; init; }
    public required string ManifestJson { get; init; }
    public required bool LicenseSectionMissing { get; init; }
    public required bool UsedCache { get; init; }
}

public static class PackLoader
{
    public static string? FindPack(string projectDir)
    {
        if (!Directory.Exists(projectDir))
            return null;
        return Directory.GetFiles(projectDir, "*.xhznpack")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static LoadedPack Load(string packPath, string projectDir)
    {
        if (!File.Exists(packPath))
            throw new FileNotFoundException("未找到发布包。", packPath);

        byte[] packBytes;
        try
        {
            packBytes = File.ReadAllBytes(packPath);
        }
        catch (IOException ex) when (IsDiskFull(ex))
        {
            throw new InvalidOperationException("磁盘空间不足，无法加载项目。", ex);
        }

        var hash = Convert.ToHexString(SHA256.HashData(packBytes)).ToLowerInvariant();
        var cacheRoot = Path.Combine(projectDir, "_cache");
        var extractDir = Path.Combine(cacheRoot, hash);
        var usedCache = Directory.Exists(extractDir) && File.Exists(Path.Combine(extractDir, "manifest.json"));

        if (!usedCache)
        {
            byte[] zipBytes;
            try
            {
                zipBytes = PackCrypto.DecryptToZip(packBytes);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("发布包无法打开，文件可能已损坏。", ex);
            }

            var tmp = extractDir + ".tmp";
            try
            {
                if (Directory.Exists(tmp))
                    Directory.Delete(tmp, true);
                Directory.CreateDirectory(cacheRoot);
                ZipPack.ExtractSafely(zipBytes, tmp);
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
                Directory.Move(tmp, extractDir);
            }
            catch (IOException ex) when (IsDiskFull(ex))
            {
                TryDelete(tmp);
                throw new InvalidOperationException("磁盘空间不足，无法加载项目。", ex);
            }
            catch
            {
                TryDelete(tmp);
                throw;
            }
        }

        var manifestPath = Path.Combine(extractDir, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidDataException("发布包缺少 manifest.json。");

        var manifestJson = ZipPack.ReadText(manifestPath);
        var manifest = PackJson.ParseManifest(manifestJson);
        if (manifest.SchemaVersion > PackCrypto.SupportedSchemaVersion)
            throw new SchemaTooNewException($"项目版本过高，请升级播放器（需要 schema {manifest.SchemaVersion}）。");

        return new LoadedPack
        {
            PackPath = packPath,
            ExtractDir = extractDir,
            Hash = hash,
            Manifest = manifest,
            ManifestJson = manifestJson,
            LicenseSectionMissing = PackJson.LicenseSectionMissing(manifestJson),
            UsedCache = usedCache
        };
    }

    public static string ResolveEntryPage(Manifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.EntryPageId)
            && manifest.Pages.Contains(manifest.EntryPageId))
            return manifest.EntryPageId;
        return manifest.Pages.Count > 0 ? manifest.Pages[0] : "";
    }

    private static bool IsDiskFull(IOException ex)
    {
        var code = ex.HResult & 0xFFFF;
        return code is 0x27 or 0x70 or 0x39;
    }

    private static void TryDelete(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch { /* ignore */ }
    }
}

public sealed class SchemaTooNewException(string message) : Exception(message);
