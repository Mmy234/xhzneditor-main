using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Xhzn.PackFormat;
using XhznPlayer.Services;

namespace XhznPlayer;

public partial class MainWindow : Window
{
    private readonly LocalConfig _config = LocalConfig.Load();
    private readonly KioskService _kiosk = new();
    private readonly List<DateTime> _exitClicks = [];
    private readonly DispatcherTimer _volumeHudTimer;
    private LoadedPack? _pack;
    private string _exitPassword = "";
    private bool _kioskOn;
    private bool _webReady;
    private Window? _exitOverlay;
    private MonitorInfo? _monitor;

    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"播放器版本 {AppPaths.Version}";
        _volumeHudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _volumeHudTimer.Tick += (_, _) =>
        {
            _volumeHudTimer.Stop();
            VolumeHud.Visibility = Visibility.Collapsed;
        };
        _kiosk.VolumeKey += OnVolumeKey;
        Loaded += OnLoaded;
        Closing += (_, _) => Cleanup();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsurePlaceholder();
            ApplyDisplay(_config.Display, enterKiosk: false);
            await StartAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("启动失败", ex);
            ShowMessage("播放器启动失败，请查看日志。");
        }
    }

    private async Task StartAsync()
    {
        var projectDir = _config.ProjectDir;
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "_cache"));
        ProjectDirText.Text = projectDir;

        var packPath = PackLoader.FindPack(projectDir);
        if (packPath == null)
        {
            Logger.Info("固定目录无发布包，显示无项目页");
            ShowEmpty(projectDir);
            ApplyDisplay(_config.Display, enterKiosk: false);
            return;
        }

        ShowLoading("正在打开发布包…");
        ApplyDisplay(_config.Display, enterKiosk: true);

        LoadedPack pack;
        try
        {
            pack = await Task.Run(() => PackLoader.Load(packPath, projectDir));
        }
        catch (SchemaTooNewException ex)
        {
            Logger.Warn(ex.Message);
            ShowMessage(ex.Message);
            return;
        }
        catch (InvalidDataException ex)
        {
            Logger.Error("解包失败", ex);
            ShowMessage(ex.Message);
            return;
        }
        catch (InvalidOperationException ex)
        {
            Logger.Error("加载失败", ex);
            ShowMessage(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            Logger.Error("加载发布包失败", ex);
            ShowMessage("发布包无法打开，文件可能已损坏。");
            return;
        }

        _pack = pack;
        Logger.Info($"包 {pack.Manifest.Id} hash={pack.Hash} cache={pack.UsedCache}");

        var license = LicenseService.Evaluate(pack);
        if (license.Decision is LicenseDecision.Expired or LicenseDecision.MachineMismatch)
        {
            ShowMessage(license.Message);
            return;
        }

        LicenseService.BindAfterLoadStart(pack);
        AutoStartService.Apply(pack.Manifest.Player?.AutoStart == true);
        _exitPassword = pack.Manifest.Player?.ExitPassword ?? "";
        var cursorVisible = pack.Manifest.Player?.CursorVisible == true;
        _kiosk.SetCursorVisible(cursorVisible);
        TrialBanner.Visibility = license.Decision == LicenseDecision.PlayTrialWatermark
            ? Visibility.Visible
            : Visibility.Collapsed;

        ShowLoading("正在加载素材…");
        await PlayAsync(pack, license.Decision == LicenseDecision.PlayTrialWatermark, cursorVisible);
    }

    private async Task PlayAsync(LoadedPack pack, bool trial, bool cursorVisible)
    {
        if (!Directory.Exists(AppPaths.RuntimeDir) || !File.Exists(Path.Combine(AppPaths.RuntimeDir, "index.html")))
        {
            ShowMessage("播放器文件不完整，缺少 Runtime。");
            return;
        }

        try
        {
            var options = new CoreWebView2EnvironmentOptions(
                additionalBrowserArguments: "--autoplay-policy=no-user-gesture-required --disable-features=msSmartScreenProtection");
            var env = await CoreWebView2Environment.CreateAsync(
                AppPaths.WebView2FixedDir,
                AppPaths.WebViewUserData,
                options);
            await WebView.EnsureCoreWebView2Async(env);
        }
        catch (Exception ex)
        {
            Logger.Error("WebView2 初始化失败", ex);
            ShowMessage("未找到 WebView2 运行时，请重新安装播放器。");
            return;
        }

        var core = WebView.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
        try { core.Settings.IsSwipeNavigationEnabled = false; }
        catch { /* 旧运行时可能没有此属性 */ }

        core.SetVirtualHostNameToFolderMapping("runtime.xhzn", AppPaths.RuntimeDir, CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping("project.xhzn", pack.ExtractDir, CoreWebView2HostResourceAccessKind.Allow);
        core.WebMessageReceived -= OnWebMessage;
        core.WebMessageReceived += OnWebMessage;
        core.ProcessFailed += (_, e) =>
        {
            Logger.Error($"WebView2 进程失败 {e.ProcessFailedKind}");
            Dispatcher.Invoke(() => ShowMessage("页面进程已退出。"));
        };

        var url =
            "https://runtime.xhzn/index.html" +
            "?projectBase=" + Uri.EscapeDataString("https://project.xhzn") +
            "&placeholder=" + Uri.EscapeDataString("https://runtime.xhzn/placeholder.png") +
            "&trial=" + (trial ? "1" : "0") +
            "&cursor=" + (cursorVisible ? "1" : "0");

        _webReady = false;
        WebView.Visibility = Visibility.Visible;
        core.Navigate(url);

        _ = Task.Delay(20000).ContinueWith(_ =>
        {
            if (_webReady) return;
            Dispatcher.Invoke(() =>
            {
                if (!_webReady && LoadingPage.Visibility == Visibility.Visible)
                {
                    Logger.Warn("加载页超时，仍进入画面");
                    LoadingPage.Visibility = Visibility.Collapsed;
                }
            });
        });
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (string.IsNullOrWhiteSpace(msg)) return;
        if (msg.Contains("ready", StringComparison.OrdinalIgnoreCase))
        {
            _webReady = true;
            Dispatcher.Invoke(() =>
            {
                LoadingPage.Visibility = Visibility.Collapsed;
                EmptyPage.Visibility = Visibility.Collapsed;
                MessagePage.Visibility = Visibility.Collapsed;
                WebView.Visibility = Visibility.Visible;
            });
        }
        else if (msg.Contains("missing-asset", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn("缺资源: " + msg);
        }
    }

    private void ShowEmpty(string projectDir)
    {
        WebView.Visibility = Visibility.Collapsed;
        LoadingPage.Visibility = Visibility.Collapsed;
        MessagePage.Visibility = Visibility.Collapsed;
        TrialBanner.Visibility = Visibility.Collapsed;
        EmptyPage.Visibility = Visibility.Visible;
        ProjectDirText.Text = projectDir;
        FillDisplays();
    }

    private void ShowLoading(string hint)
    {
        EmptyPage.Visibility = Visibility.Collapsed;
        MessagePage.Visibility = Visibility.Collapsed;
        LoadingPage.Visibility = Visibility.Visible;
        LoadingHint.Text = hint;
    }

    private void ShowMessage(string text)
    {
        EmptyPage.Visibility = Visibility.Collapsed;
        LoadingPage.Visibility = Visibility.Collapsed;
        WebView.Visibility = Visibility.Collapsed;
        MessagePage.Visibility = Visibility.Visible;
        MessageText.Text = text;
        if (!_kioskOn)
            ApplyDisplay(_config.Display, enterKiosk: true);
    }

    private void FillDisplays()
    {
        DisplayCombo.SelectionChanged -= OnDisplayChanged;
        var monitors = DisplayService.GetMonitors();
        DisplayCombo.ItemsSource = monitors;
        DisplayCombo.DisplayMemberPath = nameof(MonitorInfo.Label);
        var idx = Math.Clamp(_config.Display, 0, monitors.Count - 1);
        DisplayCombo.SelectedIndex = idx;
        DisplayCombo.SelectionChanged += OnDisplayChanged;
    }

    private void OnDisplayChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DisplayCombo.SelectedItem is not MonitorInfo info) return;
        _config.Display = info.Index;
        _config.Save();
        ApplyDisplay(info.Index, enterKiosk: _kioskOn);
    }

    private void ApplyDisplay(int index, bool enterKiosk)
    {
        _monitor = DisplayService.Pick(index);
        DisplayService.PlaceWindow(this, _monitor);
        PlaceExitOverlay(_monitor);
        if (enterKiosk && !_kioskOn)
        {
            _kiosk.Enter(hideCursor: _pack?.Manifest.Player?.CursorVisible != true);
            _kioskOn = true;
        }
    }

    private void PlaceExitOverlay(MonitorInfo monitor)
    {
        if (_exitOverlay == null)
        {
            _exitOverlay = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                ShowActivated = false,
                Focusable = false,
                Width = 120,
                Height = 120
            };
            _exitOverlay.MouseLeftButtonDown += OnExitHotspot;
        }

        _exitOverlay.Show();
        var hwnd = new WindowInteropHelper(_exitOverlay).EnsureHandle();
        Native.SetWindowPos(
            hwnd,
            Native.HwndTopmost,
            monitor.X,
            monitor.Y,
            120,
            120,
            Native.SwpShowWindow | Native.SwpNoActivate);
    }

    private void OnExitHotspot(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.UtcNow;
        _exitClicks.RemoveAll(t => now - t > TimeSpan.FromSeconds(3));
        _exitClicks.Add(now);
        if (_exitClicks.Count < 5) return;
        _exitClicks.Clear();
        TryExit();
    }

    private void TryExit()
    {
        if (string.IsNullOrEmpty(_exitPassword))
        {
            ExitToDesktop();
            return;
        }

        var hide = _pack?.Manifest.Player?.CursorVisible != true;
        _kiosk.SetCursorVisible(true);
        try
        {
            var dlg = new PasswordWindow(_exitPassword) { Owner = this };
            if (dlg.ShowDialog() == true)
                ExitToDesktop();
        }
        finally
        {
            if (_kioskOn && hide)
                _kiosk.SetCursorVisible(false);
        }
    }

    private void ExitToDesktop()
    {
        Cleanup();
        Application.Current.Shutdown();
    }

    private void OnVolumeKey(VolumeDelta delta)
    {
        Dispatcher.Invoke(() =>
        {
            var level = VolumeService.Change(delta);
            VolumeBar.Value = level;
            VolumeHud.Visibility = Visibility.Visible;
            _volumeHudTimer.Stop();
            _volumeHudTimer.Start();
        });
    }

    private static void EnsurePlaceholder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RuntimeDir);
            if (!File.Exists(AppPaths.PlaceholderPath))
                File.WriteAllBytes(AppPaths.PlaceholderPath, PngWriter.Placeholder());
        }
        catch (Exception ex)
        {
            Logger.Error("写入占位图失败", ex);
        }
    }

    private void Cleanup()
    {
        try { _kiosk.Restore(); } catch { /* ignore */ }
        _kioskOn = false;
        try
        {
            _exitOverlay?.Close();
            _exitOverlay = null;
        }
        catch { /* ignore */ }
    }
}
