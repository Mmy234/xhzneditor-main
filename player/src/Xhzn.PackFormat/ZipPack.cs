using System.IO.Compression;
using System.Text;

namespace Xhzn.PackFormat;

public static class ZipPack
{
    public static byte[] Build(IReadOnlyDictionary<string, byte[]> files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in files)
            {
                var entry = zip.CreateEntry(name.Replace('\\', '/'), CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(bytes);
            }
        }
        return ms.ToArray();
    }

    public static void ExtractSafely(byte[] zipBytes, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var destFull = Path.GetFullPath(destDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
        using var ms = new MemoryStream(zipBytes, writable: false);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
            {
                var dir = Path.GetFullPath(Path.Combine(destDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                EnsureInside(destFull, dir);
                Directory.CreateDirectory(dir);
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(destDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            EnsureInside(destFull, target);
            var parent = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    public static string ReadText(string path) => File.ReadAllText(path, Encoding.UTF8);

    private static void EnsureInside(string destFull, string target)
    {
        if (!target.StartsWith(destFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(target.TrimEnd(Path.DirectorySeparatorChar), destFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("发布包内含非法路径。");
    }
}
