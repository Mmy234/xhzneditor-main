using System.IO;
using System.Reflection;

namespace XhznPlayer.Services;

public static class AppPaths
{
    public const string AppFolderName = "XHZN";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static string InstallDir { get; } =
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    public static string RuntimeDir => Path.Combine(InstallDir, "runtime");

    public static string PlaceholderPath => Path.Combine(RuntimeDir, "placeholder.png");

    public static string DataRoot { get; } = ResolveDataRoot();

    public static string LogsDir => Path.Combine(DataRoot, "logs");

    public static string BindingsDir => Path.Combine(DataRoot, "bindings");

    public static string TrialsDir => Path.Combine(DataRoot, "trials");

    public static string MachineFile => Path.Combine(DataRoot, "machine.json");

    public static string LocalConfigFile => Path.Combine(DataRoot, "player.local.json");

    public static string DefaultProjectDir => Path.Combine(DataRoot, "projects");

    public static string WebViewUserData => Path.Combine(DataRoot, "wv2");

    public static string? WebView2FixedDir
    {
        get
        {
            foreach (var name in new[] { "WebView2Fixed", "Microsoft.WebView2.FixedVersionRuntime" })
            {
                var dir = Path.Combine(InstallDir, name);
                if (File.Exists(Path.Combine(dir, "msedgewebview2.exe")))
                    return dir;
            }
            return null;
        }
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(BindingsDir);
        Directory.CreateDirectory(TrialsDir);
        Directory.CreateDirectory(DefaultProjectDir);
        Directory.CreateDirectory(WebViewUserData);
        Directory.CreateDirectory(Path.Combine(DefaultProjectDir, "_cache"));
    }

    private static string ResolveDataRoot()
    {
        var programData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AppFolderName);
        try
        {
            Directory.CreateDirectory(programData);
            var probe = Path.Combine(programData, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return programData;
        }
        catch
        {
            var local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);
            Directory.CreateDirectory(local);
            return local;
        }
    }
}
