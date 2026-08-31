using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using WinFinger.ViewModels;
using WinFinger.Views;

namespace WinFinger;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private TaskbarIcon? _trayIcon;
    private IslandWindow? _islandWindow;
    private AppearanceWindow? _appearanceWindow;

    public AppViewModel Model { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            LogCrash(args.Exception);
            args.Handled = true; // an appearance/menu mishap must never take the island down
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);

        _singleInstanceMutex = new Mutex(true, @"Global\WinFinger.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);

        Model.Start();

        _islandWindow = new IslandWindow(Model);
        _islandWindow.Show();

        CreateTrayIcon();

        // repro hook: WINFINGER_PICKTEST=1 fires the tray 选择图片 flow 3s after startup
        if (Environment.GetEnvironmentVariable("WINFINGER_PICKTEST") == "1")
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (_, _) => { t.Stop(); PickBackgroundImage(); };
            t.Start();
        }
        if (Environment.GetEnvironmentVariable("WINFINGER_PICKTEST") == "2")
        {
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                Model.SettingsStore.Settings.BackgroundImagePath =
                    Environment.GetEnvironmentVariable("WINFINGER_PICKTEST_FILE") ?? "";
                SetBackground("image", null);
            };
            t.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        if (_ownsMutex)
        {
            try { Model.Stop(); } catch (Exception ex) { LogCrash(ex); }
            try { _singleInstanceMutex?.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "打开 WinFinger" };
        openItem.Click += (_, _) => Model.IsExpanded = true;
        menu.Items.Add(openItem);

        var pauseItem = new System.Windows.Controls.MenuItem
        {
            Header = "暂停记录剪贴板",
            IsCheckable = true,
            IsChecked = Model.ClipboardMonitor.IsPaused
        };
        pauseItem.Click += (_, _) => Model.ClipboardMonitor.IsPaused = pauseItem.IsChecked;
        menu.Items.Add(pauseItem);

        var clearItem = new System.Windows.Controls.MenuItem { Header = "清空剪贴板历史" };
        clearItem.Click += (_, _) => Model.ClipboardStore.Clear();
        menu.Items.Add(clearItem);

        var appearanceItem = new System.Windows.Controls.MenuItem { Header = "外观设置…" };
        appearanceItem.Click += (_, _) => OpenAppearanceWindow();
        menu.Items.Add(appearanceItem);

        var bgMenu = new System.Windows.Controls.MenuItem { Header = "岛背景" };
        var bgGlass = new System.Windows.Controls.MenuItem { Header = "动态玻璃" };
        bgGlass.Click += (_, _) => SetBackground("glass", null);
        bgMenu.Items.Add(bgGlass);
        (string name, string hex)[] presets =
        {
            ("经典深灰", "#1A1A22"), ("纯黑", "#0A0A0F"), ("深蓝", "#16283E"),
            ("深紫", "#1D1440"), ("酒红", "#3D0F14"), ("墨绿", "#0F3324"),
            ("暖棕", "#33270F"), ("青黛", "#0E3338")
        };
        foreach (var (name, hex) in presets)
        {
            var item = new System.Windows.Controls.MenuItem { Header = name };
            item.Click += (_, _) => SetBackground("color", hex);
            bgMenu.Items.Add(item);
        }
        var bgImage = new System.Windows.Controls.MenuItem { Header = "选择图片…" };
        bgImage.Click += (_, _) => PickBackgroundImage();
        bgMenu.Items.Add(bgImage);
        menu.Items.Add(bgMenu);

        var autoStartItem = new System.Windows.Controls.MenuItem
        {
            Header = "开机自启动",
            IsCheckable = true,
            IsChecked = Model.SettingsStore.Settings.AutoStart
        };
        autoStartItem.Click += (_, _) => Model.SettingsStore.SetAutoStart(autoStartItem.IsChecked);
        menu.Items.Add(autoStartItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var quitItem = new System.Windows.Controls.MenuItem { Header = "退出 WinFinger" };
        quitItem.Click += (_, _) => Shutdown();
        menu.Items.Add(quitItem);

        _trayIcon = new TaskbarIcon
        {
            Icon = CreatePillIcon(),
            ToolTipText = "WinFinger",
            ContextMenu = menu
        };
        _trayIcon.TrayLeftMouseUp += (_, _) => Model.ToggleExpanded();
    }

    private void PickBackgroundImage()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
                Title = "选择岛背景图片"
            };
            // the island window is NOACTIVATE and the tray menu's host is transient,
            // so the dialog needs a real activatable owner or it can't take focus
            var owner = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Opacity = 0,
                ShowInTaskbar = false,
                Width = 1,
                Height = 1,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            owner.Show();
            owner.Activate();
            try
            {
                if (dlg.ShowDialog(owner) != true) return;
            }
            finally
            {
                owner.Close();
            }
            Model.SettingsStore.Settings.BackgroundImagePath = dlg.FileName;
            SetBackground("image", null);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
        }
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinFinger");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch { }
    }

    private void SetBackground(string mode, string? color)
    {
        var s = Model.SettingsStore.Settings;
        s.BackgroundMode = mode;
        if (color is not null) s.BackgroundColor = color;
        Model.SettingsStore.Save();
        _islandWindow?.ApplyBackground();
    }

    private void OpenAppearanceWindow()
    {
        if (_appearanceWindow is { IsLoaded: true })
        {
            _appearanceWindow.Activate();
            return;
        }
        if (_islandWindow is null) return;
        _appearanceWindow = new AppearanceWindow(Model, _islandWindow);
        _appearanceWindow.Closed += (_, _) => _appearanceWindow = null;
        _appearanceWindow.Show();
        _appearanceWindow.Activate();
    }

    /// <summary>Draws the island pill as a 32x32 tray icon at runtime (no .ico asset needed).</summary>
    private static Icon CreatePillIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            var rect = new Rectangle(2, 10, 28, 12);
            int r = rect.Height;
            path.AddArc(rect.X, rect.Y, r, r, 90, 180);
            path.AddArc(rect.Right - r, rect.Y, r, r, 270, 180);
            path.CloseFigure();
            using var fill = new SolidBrush(System.Drawing.Color.FromArgb(255, 20, 20, 22));
            using var stroke = new Pen(System.Drawing.Color.FromArgb(200, 235, 235, 240), 1.6f);
            g.FillPath(fill, path);
            g.DrawPath(stroke, path);
        }
        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
