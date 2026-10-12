using System.Windows;
using System.Windows.Threading;
using XhznPlayer.Services;

namespace XhznPlayer;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppPaths.EnsureDirectories();
        Logger.Info($"启动 XHZN播放器 {AppPaths.Version}");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TryRestoreShell();
        Logger.Info("退出");
        base.OnExit(e);
    }

    internal static void TryRestoreShell()
    {
        try
        {
            Native.ShowWindow(Native.FindWindow("Shell_TrayWnd", null), Native.SwShow);
            Native.ShowWindow(Native.FindWindow("Shell_SecondaryTrayWnd", null), Native.SwShow);
            for (var i = 0; i < 64 && Native.ShowCursor(true) < 0; i++) { }
        }
        catch { /* ignore */ }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("未处理 UI 异常", e.Exception);
        e.Handled = true;
        MessageBox.Show("播放器遇到错误，已写入日志。", "XHZN播放器", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Logger.Error("未处理异常", e.ExceptionObject as Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("后台任务异常", e.Exception);
        e.SetObserved();
    }
}
