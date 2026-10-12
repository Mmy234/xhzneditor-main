using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace XhznPlayer.Services;

public sealed class KioskService : IDisposable
{
    private Native.LowLevelKeyboardProc? _proc;
    private IntPtr _hook;
    private readonly List<IntPtr> _hidden = [];
    private int? _edgeSwipePrev;
    private int? _tlCornerPrev;
    private int? _trCornerPrev;
    private bool _cursorHidden;
    private int _cursorHideCount;
    private bool _restoreHooked;

    public event Action<VolumeDelta>? VolumeKey;

    public void Enter(bool hideCursor)
    {
        if (!_restoreHooked)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Restore();
            System.Windows.Application.Current.SessionEnding += (_, _) => Restore();
            _restoreHooked = true;
        }
        HideTaskbars();
        InstallKeyHook();
        DisableEdgeUi();
        SetCursorVisible(!hideCursor);
    }

    public void SetCursorVisible(bool visible)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Mouse.OverrideCursor = visible ? null : Cursors.None;
        });

        if (visible)
        {
            while (_cursorHideCount > 0)
            {
                Native.ShowCursor(true);
                _cursorHideCount--;
            }
            _cursorHidden = false;
        }
        else if (!_cursorHidden)
        {
            var v = Native.ShowCursor(false);
            _cursorHideCount++;
            while (v >= 0)
            {
                v = Native.ShowCursor(false);
                _cursorHideCount++;
            }
            _cursorHidden = true;
        }
    }

    public void Dispose()
    {
        Restore();
        GC.SuppressFinalize(this);
    }

    public void Restore()
    {
        UninstallKeyHook();
        RestoreTaskbars();
        RestoreEdgeUi();
        SetCursorVisible(true);
    }

    private void HideTaskbars()
    {
        foreach (var cls in new[] { "Shell_TrayWnd", "Shell_SecondaryTrayWnd" })
        {
            var hwnd = Native.FindWindow(cls, null);
            if (hwnd != IntPtr.Zero)
            {
                Native.ShowWindow(hwnd, Native.SwHide);
                _hidden.Add(hwnd);
            }
        }
    }

    private void RestoreTaskbars()
    {
        foreach (var hwnd in _hidden)
            Native.ShowWindow(hwnd, Native.SwShow);
        _hidden.Clear();
    }

    private void DisableEdgeUi()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ImmersiveShell\EdgeUI");
            if (key == null) return;
            _tlCornerPrev = key.GetValue("DisableTLcorner") as int?;
            _trCornerPrev = key.GetValue("DisableTRcorner") as int?;
            key.SetValue("DisableTLcorner", 1, RegistryValueKind.DWord);
            key.SetValue("DisableTRcorner", 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            Logger.Warn("无法写入 EdgeUI 注册表: " + ex.Message);
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\EdgeUI");
            if (key == null) return;
            _edgeSwipePrev = key.GetValue("AllowEdgeSwipe") as int?;
            key.SetValue("AllowEdgeSwipe", 0, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            Logger.Warn("无法禁用边缘滑动: " + ex.Message);
        }
    }

    private void RestoreEdgeUi()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\ImmersiveShell\EdgeUI");
            RestoreDword(key, "DisableTLcorner", _tlCornerPrev);
            RestoreDword(key, "DisableTRcorner", _trCornerPrev);
        }
        catch { /* ignore */ }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\EdgeUI");
            RestoreDword(key, "AllowEdgeSwipe", _edgeSwipePrev);
        }
        catch { /* ignore */ }
    }

    private static void RestoreDword(RegistryKey? key, string name, int? prev)
    {
        if (key == null) return;
        if (prev == null) key.DeleteValue(name, throwOnMissingValue: false);
        else key.SetValue(name, prev.Value, RegistryValueKind.DWord);
    }

    private void InstallKeyHook()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = Hook;
        using var proc = System.Diagnostics.Process.GetCurrentProcess();
        using var mod = proc.MainModule;
        _hook = Native.SetWindowsHookEx(Native.WhKeyboardLl, _proc, Native.GetModuleHandle(mod?.ModuleName), 0);
        if (_hook == IntPtr.Zero)
            Logger.Warn("键盘钩子安装失败: " + Marshal.GetLastWin32Error());
    }

    private void UninstallKeyHook()
    {
        if (_hook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _proc = null;
    }

    private IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == Native.WmKeyDown || wParam == Native.WmSysKeyDown))
        {
            var info = Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
            var vk = info.VkCode;
            var alt = (Native.GetAsyncKeyState(Native.VkMenu) & 0x8000) != 0
                      || (Native.GetAsyncKeyState(Native.VkLmenu) & 0x8000) != 0
                      || (Native.GetAsyncKeyState(Native.VkRmenu) & 0x8000) != 0;
            var ctrl = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0;

            if (vk is Native.VkVolumeUp or Native.VkVolumeDown or Native.VkVolumeMute)
            {
                var delta = vk == Native.VkVolumeUp ? VolumeDelta.Up
                    : vk == Native.VkVolumeDown ? VolumeDelta.Down
                    : VolumeDelta.Mute;
                try { VolumeKey?.Invoke(delta); } catch { /* ignore */ }
                return 1;
            }

            if (vk is Native.VkLwin or Native.VkRwin)
                return 1;
            if (alt && vk == Native.VkTab)
                return 1;
            if (alt && vk == Native.VkEscape)
                return 1;
            if (ctrl && vk == Native.VkEscape)
                return 1;
            if (alt && vk == Native.VkF4)
                return 1;
            // Ctrl+Shift+Esc 留给任务管理器，避免整屏置顶后无法脱身
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }
}

public enum VolumeDelta { Up, Down, Mute }
