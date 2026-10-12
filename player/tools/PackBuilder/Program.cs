using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Xhzn.PackFormat;

if (args.Length > 0)
{
    foreach (var path in args)
    {
        var zip = PackCrypto.DecryptToZip(File.ReadAllBytes(path));
        using var za = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        Console.WriteLine(path);
        Console.WriteLine("entries=" + za.Entries.Count);
        foreach (var e in za.Entries)
            Console.WriteLine("  " + e.FullName + "  " + e.Length);
        if (za.GetEntry("manifest.json") == null)
            throw new InvalidDataException("缺少 manifest.json");
        Console.WriteLine("打开成功");
    }
    return;
}

var root = FindRepoRoot();
var samples = Path.Combine(root, "samples");
var golden = Path.Combine(samples, "golden");
var assets = Path.Combine(golden, "assets");
var pages = Path.Combine(golden, "pages");
var runtime = Path.Combine(root, "runtime");
Directory.CreateDirectory(assets);
Directory.CreateDirectory(pages);
Directory.CreateDirectory(runtime);

File.WriteAllBytes(Path.Combine(runtime, "placeholder.png"), PngWriter.Placeholder());
File.WriteAllBytes(Path.Combine(assets, "hero.png"), PngWriter.WriteRgb(800, 400, (x, y) =>
{
    var t = x / 800f;
    var r = (byte)(20 + t * 40);
    var g = (byte)(80 + t * 50);
    var b = (byte)(140 + (1 - t) * 40);
    var frame = x < 6 || y < 6 || x > 793 || y > 393;
    return frame ? ((byte)220, (byte)220, (byte)230) : (r, g, b);
}));
File.WriteAllBytes(Path.Combine(assets, "btn.png"), PngWriter.WriteRgb(280, 80, (_, _) => (45, 120, 220)));
File.WriteAllBytes(Path.Combine(assets, "btn-down.png"), PngWriter.WriteRgb(280, 80, (_, _) => (30, 90, 180)));

var videoPath = Path.Combine(assets, "clip.mp4");
if (!File.Exists(videoPath) || new FileInfo(videoPath).Length < 100)
{
    var downloaded = await TryDownloadVideo(videoPath);
    if (!downloaded)
        Console.WriteLine("未下载到样例视频，golden 包将引用 clip.mp4（缺文件时 Runtime 显示占位图）。");
}

WriteJson(Path.Combine(golden, "project.json"), new { fonts = Array.Empty<object>() });
WriteJson(Path.Combine(pages, "home.json"), new
{
    id = "home",
    name = "首页",
    background = new { type = "color", value = "#102040" },
    idle = new { seconds = 0, pageId = "home" },
    defaultTransition = new { type = "fade", dur = 0.3 },
    components = new object[]
    {
        new
        {
            id = "t1", name = "标题", type = "text",
            x = 200, y = 48, w = 1520, h = 80, rotation = 0, z = 2, visible = true,
            onClick = (object?)null,
            props = new
            {
                text = "欢迎查询",
                fontFamily = "Microsoft YaHei",
                fontSize = 64,
                color = "#FFFFFF",
                fontWeight = 700,
                italic = false,
                align = "left",
                valign = "middle",
                lineHeight = 1.4,
                letterSpacing = 2
            }
        },
        new
        {
            id = "img1", name = "主图", type = "image",
            x = 200, y = 160, w = 800, h = 400, rotation = 0, z = 1, visible = true,
            onClick = (object?)null,
            props = new { src = "assets/hero.png", fit = "cover" }
        },
        new
        {
            id = "btn1", name = "下一页", type = "button",
            x = 200, y = 600, w = 280, h = 80, rotation = 0, z = 3, visible = true,
            onClick = new { action = "gotoPage", pageId = "p2", transition = new { type = "fade", dur = 0.3 } },
            props = new { srcUp = "assets/btn.png", srcDown = "assets/btn-down.png" }
        }
    }
});
WriteJson(Path.Combine(pages, "p2.json"), new
{
    id = "p2",
    name = "第二页",
    background = new { type = "color", value = "#1A1028" },
    idle = new { seconds = 8, pageId = "home" },
    defaultTransition = new { type = "fade", dur = 0.3 },
    components = new object[]
    {
        new
        {
            id = "t2", name = "副标题", type = "text",
            x = 80, y = 40, w = 800, h = 64, rotation = 0, z = 2, visible = true,
            onClick = (object?)null,
            props = new
            {
                text = "第二页",
                fontFamily = "Microsoft YaHei",
                fontSize = 48,
                color = "#FFFFFF",
                fontWeight = 400,
                italic = false,
                align = "left",
                valign = "middle",
                lineHeight = 1.4,
                letterSpacing = 0
            }
        },
        new
        {
            id = "v1", name = "演示视频", type = "video",
            x = 80, y = 120, w = 960, h = 540, rotation = 0, z = 1, visible = true,
            onClick = (object?)null,
            onEnded = (object?)null,
            props = new
            {
                src = new[] { "assets/clip.mp4" },
                loopMode = "single",
                intervalSeconds = 0,
                muted = false,
                autoplay = true
            }
        },
        new
        {
            id = "btn2", name = "回首页", type = "button",
            x = 80, y = 690, w = 280, h = 80, rotation = 0, z = 3, visible = true,
            onClick = new { action = "gotoHome", transition = new { type = "slideLeft", dur = 0.3 } },
            props = new { srcUp = "assets/btn.png", srcDown = "assets/btn-down.png" }
        },
        new
        {
            id = "missing", name = "缺图", type = "image",
            x = 1100, y = 120, w = 240, h = 135, rotation = 0, z = 1, visible = true,
            onClick = (object?)null,
            props = new { src = "assets/does-not-exist.png", fit = "cover" }
        }
    }
});

var files = Collect(golden);
WritePack(samples, "golden.xhznpack", files, Manifest("demo-001", 1, License("2027-12-31", 365), false));
WritePack(samples, "golden-nolicense.xhznpack", files, Manifest("demo-nolicense", 1, null, false));
WritePack(samples, "golden-expired.xhznpack", files, Manifest("demo-expired", 1, License("2020-01-01", null), false));
WritePack(samples, "golden-schema2.xhznpack", files, Manifest("demo-schema2", 2, License("2027-12-31", 365), false));

foreach (var n in new[] { "golden.xhznpack", "golden-nolicense.xhznpack", "golden-expired.xhznpack", "golden-schema2.xhznpack" })
{
    var zip = PackCrypto.DecryptToZip(File.ReadAllBytes(Path.Combine(samples, n)));
    using var za = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
    if (za.GetEntry("manifest.json") == null || za.GetEntry("pages/home.json") == null)
        throw new InvalidDataException("包内容不完整: " + n);
    Console.WriteLine("校验通过 " + n);
}

Console.WriteLine("已生成:");
Console.WriteLine("  " + Path.Combine(samples, "golden.xhznpack"));
Console.WriteLine("  " + Path.Combine(samples, "golden-nolicense.xhznpack"));
Console.WriteLine("  " + Path.Combine(samples, "golden-expired.xhznpack"));
Console.WriteLine("  " + Path.Combine(samples, "golden-schema2.xhznpack"));
return;

static object? License(string? expire, int? trial) => new { expireDate = expire, trialDays = trial };

static Dictionary<string, object?> Manifest(string id, int schema, object? license, bool cursor)
{
    var map = new Dictionary<string, object?>
    {
        ["schemaVersion"] = schema,
        ["publishVersion"] = 1,
        ["id"] = id,
        ["name"] = "样例查询",
        ["design"] = new { width = 1920, height = 1080 },
        ["pages"] = new[] { "home", "p2" },
        ["entryPageId"] = "home",
        ["player"] = new { autoStart = false, cursorVisible = cursor, exitPassword = "" }
    };
    if (license != null)
        map["license"] = license;
    return map;
}

static void WritePack(string samplesDir, string name, Dictionary<string, byte[]> files, Dictionary<string, object?> manifest)
{
    var copy = new Dictionary<string, byte[]>(files, StringComparer.OrdinalIgnoreCase);
    var json = JsonSerializer.Serialize(manifest, JsonUtf8(ignoreNull: true));
    copy["manifest.json"] = Encoding.UTF8.GetBytes(json);
    if (name == "golden.xhznpack")
        File.WriteAllText(Path.Combine(samplesDir, "golden", "manifest.json"), json);
    var zip = ZipPack.Build(copy);
    var pack = PackCrypto.EncryptZip(zip);
    File.WriteAllBytes(Path.Combine(samplesDir, name), pack);
}

static Dictionary<string, byte[]> Collect(string dir)
{
    var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    foreach (var path in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(dir, path).Replace('\\', '/');
        if (rel.Equals("manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
        files[rel] = File.ReadAllBytes(path);
    }
    return files;
}

static void WriteJson(string path, object obj) =>
    File.WriteAllText(path, JsonSerializer.Serialize(obj, JsonUtf8()));

static JsonSerializerOptions JsonUtf8(bool ignoreNull = false) => new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = ignoreNull
        ? System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        : System.Text.Json.Serialization.JsonIgnoreCondition.Never
};

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "Doc", "player-v1.md")) ||
            Directory.Exists(Path.Combine(dir.FullName, "runtime")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}

static async Task<bool> TryDownloadVideo(string dest)
{
    var urls = new[]
    {
        "https://interactive-examples.mdn.mozilla.net/media/cc0-videos/flower.mp4",
        "https://www.w3schools.com/html/mov_bbb.mp4"
    };
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    foreach (var url in urls)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            if (bytes.Length > 1000)
            {
                File.WriteAllBytes(dest, bytes);
                Console.WriteLine("已下载样例视频 " + url);
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("下载失败 " + url + " : " + ex.Message);
        }
    }
    return false;
}
