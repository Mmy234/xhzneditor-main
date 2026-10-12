using System.IO;
using Microsoft.Win32;

namespace XhznPlayer.Services;

public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "XhznPlayer";

    public static void Apply(bool autoStart)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;
            if (autoStart)
            {
                var exe = Environment.ProcessPath ?? Path.Combine(AppPaths.InstallDir, "XhznPlayer.exe");
                key.SetValue(ValueName, $"\"{exe}\"");
                Logger.Info("已登记开机自启");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Logger.Info("已取消开机自启");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("设置开机自启失败", ex);
        }
    }
}
