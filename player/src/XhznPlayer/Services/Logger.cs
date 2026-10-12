using System.IO;
using System.Text;

namespace XhznPlayer.Services;

public static class Logger
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDir);
            var path = Path.Combine(AppPaths.LogsDir, $"player-{DateTime.Now:yyyyMMdd}.log");
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(' ').Append(level).Append(' ').Append(message);
            if (ex != null)
            {
                sb.AppendLine();
                sb.Append(ex);
            }
            sb.AppendLine();
            lock (Gate)
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // 日志失败不能影响播放
        }
    }
}
