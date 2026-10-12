using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace XhznPlayer.Services;

public sealed class MonitorInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool IsPrimary { get; init; }

    public string Label =>
        $"显示器 {Index + 1}  {Width}×{Height}{(IsPrimary ? "  (主屏)" : "")}";
}

public static class DisplayService
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref Native.Rect _, IntPtr data) =>
        {
            var mi = new Native.MonitorInfoEx { Size = Marshal.SizeOf<Native.MonitorInfoEx>() };
            if (Native.GetMonitorInfo(hMon, ref mi))
            {
                list.Add(new MonitorInfo
                {
                    Index = list.Count,
                    Name = mi.DeviceName,
                    X = mi.Monitor.Left,
                    Y = mi.Monitor.Top,
                    Width = mi.Monitor.Width,
                    Height = mi.Monitor.Height,
                    IsPrimary = (mi.Flags & 1) != 0
                });
            }
            return true;
        }, IntPtr.Zero);

        if (list.Count == 0)
        {
            list.Add(new MonitorInfo
            {
                Index = 0,
                Name = "DISPLAY1",
                X = 0,
                Y = 0,
                Width = (int)SystemParameters.PrimaryScreenWidth,
                Height = (int)SystemParameters.PrimaryScreenHeight,
                IsPrimary = true
            });
        }

        return list;
    }

    public static MonitorInfo Pick(int index)
    {
        var monitors = GetMonitors();
        if (index < 0 || index >= monitors.Count)
            return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        return monitors[index];
    }

    public static void PlaceWindow(Window window, MonitorInfo monitor)
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.NoResize;
        window.WindowState = WindowState.Normal;
        window.Left = 0;
        window.Top = 0;
        window.Width = 16;
        window.Height = 16;
        window.Show();

        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        Native.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            monitor.X,
            monitor.Y,
            monitor.Width,
            monitor.Height,
            Native.SwpShowWindow);
    }
}
