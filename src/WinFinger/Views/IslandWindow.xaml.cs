using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using WinFinger.Controls;
using WinFinger.Interop;
using WinFinger.ViewModels;

namespace WinFinger.Views;

public partial class IslandWindow : Window
{
    // Island geometry (DIP)
    private const double CompactWidth = 300;
    private const double CompactHeight = 36;
    private const double CompactRadius = 18;
    private const double ExpandedWidth = 720;
    private const double ExpandedHeight = 480;
    private const double ExpandedRadius = 28;

    private const double NotificationWidth = 430;
    private const double HoverWidth = 390;

    private readonly AppViewModel _model;
    private IntPtr _hwnd;
    private IntPtr _mouseHook;
    private NativeMethods.LowLevelMouseProc? _mouseProc; // field: keeps delegate alive against GC
    private readonly System.Windows.Threading.DispatcherTimer _notificationTimer;
    private bool _notificationShowing;
    private bool _hovering;

    // ghost mode (fade + click-through when the cursor is far away)
    private readonly System.Windows.Threading.DispatcherTimer _ghostTimer;
    private bool _ghosted;
    private const double GhostEnterDistance = 160; // px, become ghost beyond this
    private const double GhostExitDistance = 100;  // px, solidify within this

    // self-made frosted glass (live capture behind the island)
    private LiveGlassCapture? _glass;
    private System.Windows.Threading.DispatcherTimer? _glassTimer;
    private bool _morphing; // size animation in flight: skip captures so they don't fight for frames

    // horizontal drag reposition
    private bool _dragging;
    private bool _dragArmed;
    private System.Windows.Point _dragStartScreen;
    private double _dragStartLeft;
    private double _dragStartTop;
    private double? _preExpandTop; // set when the window is shifted up to fit the expanded panel

    public IslandWindow(AppViewModel model)
    {
        _model = model;
        InitializeComponent();
        DataContext = model;
        CompactView.Initialize(model);
        ExpandedView.Initialize(model);

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) =>
        {
            PositionAtTopCenter();
            _model.ClipboardMonitor.Attach(this);
            _glass = new LiveGlassCapture();
            GlassBrush.ImageSource = _glass.Bitmap;
            _glassTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(160)
            };
            _glassTimer.Tick += (_, _) => CaptureGlass();
            SetLiveGlass(_model.SettingsStore.Settings.LiveGlassEnabled);
        };
        PreviewKeyDown += OnPreviewKeyDown;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        model.PropertyChanged += OnModelPropertyChanged;

        _notificationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(2600)
        };
        _notificationTimer.Tick += (_, _) => HideNotification();
        model.Notifications.NotificationPosted += OnNotificationPosted;
        model.Media.PropertyChanged += OnMediaChangedForGlow;

        _ghostTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _ghostTimer.Tick += (_, _) => UpdateGhostState();
        _ghostTimer.Start();

        StartGlintBreathing();

        // periodic working-set trim keeps the Task Manager footprint honest for a tray-style app
        var trimTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        trimTimer.Tick += (_, _) => { if (!_model.IsExpanded) TrimWorkingSet(); };
        trimTimer.Start();

        // first trim shortly after startup, once JIT/first-render churn is over
        var firstTrim = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        firstTrim.Tick += (_, _) => { firstTrim.Stop(); TrimWorkingSet(); };
        firstTrim.Start();
    }

    private static void TrimWorkingSet()
    {
        GC.Collect(2, GCCollectionMode.Optimized);
        GC.WaitForPendingFinalizers();
        NativeMethods.SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle,
            new IntPtr(-1), new IntPtr(-1));
    }

    /// <summary>Counter-phased opacity loops so light appears to drift around the glass rim.</summary>
    private void StartGlintBreathing()
    {
        var breathe = new DoubleAnimation(0.2, 0.95, TimeSpan.FromSeconds(2.8))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        var counter = new DoubleAnimation(0.9, 0.15, TimeSpan.FromSeconds(2.8))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        // slow ambience: no need to re-render the shadowed island subtree at 60fps
        Timeline.SetDesiredFrameRate(breathe, 20);
        Timeline.SetDesiredFrameRate(counter, 20);
        GlintA.BeginAnimation(OpacityProperty, breathe);
        GlintB.BeginAnimation(OpacityProperty, counter);
    }

    // ── Ghost mode: far cursor → translucent + click-through, near cursor → solid ──

    private void UpdateGhostState()
    {
        if (_hwnd == IntPtr.Zero || !IslandBorder.IsLoaded) return;

        if (_model.IsExpanded || _notificationShowing || _hovering || _dragging)
        {
            if (_ghosted) SetGhosted(false);
            return;
        }

        if (!NativeMethods.GetCursorPos(out var cursor)) return;
        Rect bounds;
        try
        {
            var topLeft = IslandBorder.PointToScreen(new Point(0, 0));
            var bottomRight = IslandBorder.PointToScreen(new Point(IslandBorder.ActualWidth, IslandBorder.ActualHeight));
            bounds = new Rect(topLeft, bottomRight);
        }
        catch
        {
            return;
        }
        double dx = Math.Max(Math.Max(bounds.Left - cursor.X, cursor.X - bounds.Right), 0);
        double dy = Math.Max(Math.Max(bounds.Top - cursor.Y, cursor.Y - bounds.Bottom), 0);
        double distance = Math.Sqrt(dx * dx + dy * dy);

        if (!_ghosted && distance > GhostEnterDistance) SetGhosted(true);
        else if (_ghosted && distance < GhostExitDistance) SetGhosted(false);
    }

    private void SetGhosted(bool ghosted)
    {
        _ghosted = ghosted;
        int style = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        style = ghosted ? style | NativeMethods.WS_EX_TRANSPARENT : style & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, style);
        IslandBorder.BeginAnimation(OpacityProperty,
            new DoubleAnimation(ghosted ? 0.4 : 1.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        // faded island doesn't need ambience: stop all recurring animation while ghosted
        if (ghosted)
        {
            GlintA.BeginAnimation(OpacityProperty, null);
            GlintB.BeginAnimation(OpacityProperty, null);
            GlintA.Opacity = 0.3;
            GlintB.Opacity = 0.3;
        }
        else
        {
            StartGlintBreathing();
            CaptureGlass();
        }
    }

    /// <summary>Toggles the live-capture glass (heavier GPU) vs. plain static glass.</summary>
    public void SetLiveGlass(bool enabled)
    {
        if (_glassTimer is null) return;
        GlassLayer.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        if (enabled)
        {
            _glassTimer.Start();
            CaptureGlass();
        }
        else
        {
            _glassTimer.Stop();
        }
    }

    /// <summary>One glass frame: grab what's behind IslandBorder (device px) into the ImageBrush.</summary>
    private void CaptureGlass()
    {
        if (_glass is null || !IslandBorder.IsLoaded || _ghosted || _morphing) return;
        try
        {
            var topLeft = IslandBorder.PointToScreen(new Point(0, 0));
            var bottomRight = IslandBorder.PointToScreen(new Point(IslandBorder.ActualWidth, IslandBorder.ActualHeight));
            _glass.Capture((int)topLeft.X, (int)topLeft.Y,
                (int)(bottomRight.X - topLeft.X), (int)(bottomRight.Y - topLeft.Y));
        }
        catch
        {
            // island not on screen yet
        }
    }

    // ── Cover-color glow (pulses while music plays) ──

    private void OnMediaChangedForGlow(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Services.MediaService.IsPlaying) or nameof(Services.MediaService.AccentColor))
            UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (_model.Media.IsPlaying)
        {
            // adaptive tint: bleed the album accent into the glass
            TintBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty,
                new ColorAnimation(_model.Media.AccentColor, TimeSpan.FromMilliseconds(600)));
            TintLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0.12, TimeSpan.FromMilliseconds(600)));
            IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty,
                new ColorAnimation(_model.Media.AccentColor, TimeSpan.FromMilliseconds(600)));
            var pulse = new DoubleAnimation(0.45, 0.85, TimeSpan.FromMilliseconds(1600))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Timeline.SetDesiredFrameRate(pulse, 20);
            IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, pulse);
        }
        else
        {
            TintLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(600)));
            IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty,
                new ColorAnimation(System.Windows.Media.Colors.Black, TimeSpan.FromMilliseconds(600)));
            IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
                new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(600)));
        }
    }

    private void OnIslandMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(IslandBorder);

        // free drag: the island floats anywhere on screen
        if (_dragArmed && e.LeftButton == MouseButtonState.Pressed)
        {
            var screen = IslandBorder.PointToScreen(position);
            double deltaX = screen.X - _dragStartScreen.X;
            double deltaY = screen.Y - _dragStartScreen.Y;
            if (!_dragging && (Math.Abs(deltaX) > 4 || Math.Abs(deltaY) > 4)) _dragging = true;
            if (_dragging)
            {
                // PointToScreen returns device px; convert delta to DIP
                var source = PresentationSource.FromVisual(this);
                double scale = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                Left = _dragStartLeft + deltaX / scale;
                Top = _dragStartTop + deltaY / scale;
                ClampPosition();
                CaptureGlass();
            }
        }
    }

    /// <summary>Sweeps a diagonal sheen band across the island (liquid glass "reacts" moment).</summary>
    private void PlaySheen()
    {
        double travel = IslandBorder.ActualWidth + 300;
        SheenBand.Opacity = 1;
        SheenTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new DoubleAnimation(-220, travel, TimeSpan.FromMilliseconds(700))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            });
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150)) { BeginTime = TimeSpan.FromMilliseconds(600) };
        SheenBand.BeginAnimation(OpacityProperty, fade);
    }

    // ── Hover pre-expand (compact state only) ──

    private void OnIslandMouseEnter(object sender, MouseEventArgs e)
    {
        if (_model.IsExpanded || _notificationShowing || _hovering) return;
        _hovering = true;
        AnimateIsland(toWidth: HoverWidth, toHeight: CompactHeight + 6, toRadius: (CompactHeight + 6) / 2,
            duration: TimeSpan.FromMilliseconds(220),
            easing: new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 });
        CompactView.SetHoverState(true);
    }

    private void OnIslandMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_hovering) return;
        _hovering = false;
        CompactView.SetHoverState(false);
        if (_model.IsExpanded || _notificationShowing) return; // another state took over
        AnimateIsland(toWidth: CompactWidth, toHeight: CompactHeight, toRadius: CompactRadius,
            duration: TimeSpan.FromMilliseconds(180),
            easing: new CubicEase { EasingMode = EasingMode.EaseOut });
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        int style = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE,
            style | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
        // keep the island out of screen captures so the live glass never captures itself
        NativeMethods.SetWindowDisplayAffinity(_hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    protected override void OnClosed(EventArgs e)
    {
        _ghostTimer.Stop();
        _glassTimer?.Stop();
        _glass?.Dispose();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        RemoveMouseHook();
        base.OnClosed(e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(PositionAtTopCenter);

    private void PositionAtTopCenter()
    {
        Left = CenteredLeft() + _model.SettingsStore.Settings.IslandOffsetX;
        Top = _model.SettingsStore.Settings.IslandOffsetY;
        ClampPosition();
    }

    private double CenteredLeft() => (SystemParameters.PrimaryScreenWidth - Width) / 2;

    private void ClampPosition()
    {
        // keep the visible island (top-centered inside the stage window) on screen
        double islandHalf = Math.Max(IslandBorder.ActualWidth, CompactWidth) / 2;
        double minX = 8 - (Width / 2 - islandHalf);
        double maxX = SystemParameters.PrimaryScreenWidth - 8 - (Width / 2 + islandHalf);
        Left = Math.Clamp(Left, minX, maxX);
        // island sits 8 DIP below the stage top; keep the compact pill fully visible
        double maxY = SystemParameters.PrimaryScreenHeight - CompactHeight - 16;
        Top = Math.Clamp(Top, -8, maxY);
    }

    // ── Click vs horizontal drag ──

    private void OnIslandMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_model.IsExpanded) return;
        _dragArmed = true;
        _dragging = false;
        _dragStartScreen = IslandBorder.PointToScreen(e.GetPosition(IslandBorder));
        _dragStartLeft = Left;
        _dragStartTop = Top;
        IslandBorder.CaptureMouse();
    }

    private void OnIslandClicked(object sender, MouseButtonEventArgs e)
    {
        if (_dragArmed)
        {
            _dragArmed = false;
            IslandBorder.ReleaseMouseCapture();
            if (_dragging)
            {
                _dragging = false;
                _model.SettingsStore.Settings.IslandOffsetX = Left - CenteredLeft();
                _model.SettingsStore.Settings.IslandOffsetY = Top;
                _model.SettingsStore.Save();
                return; // a drag is not a click
            }
        }
        if (!_model.IsExpanded)
            _model.IsExpanded = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_model.IsExpanded) return;

        if (e.Key == Key.Escape)
        {
            _model.Collapse();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            AppPage? page = e.Key switch
            {
                Key.D1 => AppPage.Clipboard,
                Key.D2 => AppPage.Media,
                Key.D3 => AppPage.Notes,
                Key.D4 => AppPage.Shortcuts,
                Key.D5 => AppPage.Pomodoro,
                _ => null
            };
            if (page is { } p)
            {
                _model.SelectedPage = p;
                e.Handled = true;
            }
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppViewModel.IsExpanded))
        {
            if (_model.IsExpanded) Expand();
            else Collapse();
        }
    }

    // ── Expand / collapse choreography ──

    // ── Notification bulge (compact-state only) ──

    private void OnNotificationPosted(Services.IslandNotification notification)
    {
        if (_model.IsExpanded) return;
        if (_hovering)
        {
            _hovering = false;
            CompactView.SetHoverState(false);
        }
        NotificationIcon.Text = notification.Icon;
        NotificationText.Text = notification.Message;
        _notificationTimer.Stop();
        _notificationTimer.Start();
        if (_notificationShowing) return;
        _notificationShowing = true;

        AnimateIsland(toWidth: NotificationWidth, toHeight: CompactHeight, toRadius: CompactRadius,
            duration: TimeSpan.FromMilliseconds(240),
            easing: new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 });

        NotificationView.Visibility = Visibility.Visible;
        FadeTo(CompactView, 0, TimeSpan.FromMilliseconds(80), () => CompactView.Visibility = Visibility.Collapsed);
        NotificationView.Opacity = 0;
        NotificationView.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)) { BeginTime = TimeSpan.FromMilliseconds(120) });
        PlaySheen();
    }

    private void HideNotification()
    {
        _notificationTimer.Stop();
        if (!_notificationShowing) return;
        _notificationShowing = false;
        if (_model.IsExpanded) return; // expand animation already took over

        AnimateIsland(toWidth: CompactWidth, toHeight: CompactHeight, toRadius: CompactRadius,
            duration: TimeSpan.FromMilliseconds(200),
            easing: new CubicEase { EasingMode = EasingMode.EaseInOut });

        CompactView.Visibility = Visibility.Visible;
        FadeTo(NotificationView, 0, TimeSpan.FromMilliseconds(80), () => NotificationView.Visibility = Visibility.Collapsed);
        CompactView.Opacity = 0;
        CompactView.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)) { BeginTime = TimeSpan.FromMilliseconds(100) });
    }

    private void Expand()
    {
        if (_hovering)
        {
            _hovering = false;
            CompactView.SetHoverState(false);
        }
        if (_notificationShowing)
        {
            _notificationTimer.Stop();
            _notificationShowing = false;
            NotificationView.Visibility = Visibility.Collapsed;
            NotificationView.Opacity = 0;
        }
        SetNoActivate(false);
        Activate();
        Focus();

        // dragged low on screen: shift up so the expanded panel fits, restore on collapse
        double needed = 8 + ExpandedHeight + 12;
        if (Top + needed > SystemParameters.PrimaryScreenHeight)
        {
            _preExpandTop = Top;
            Top = SystemParameters.PrimaryScreenHeight - needed;
        }

        AnimateIsland(toWidth: ExpandedWidth, toHeight: ExpandedHeight, toRadius: ExpandedRadius,
            duration: TimeSpan.FromMilliseconds(280),
            easing: new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.32 });

        // Content crossfade: compact out fast, expanded in after ~70% of the resize.
        ExpandedView.Visibility = Visibility.Visible;
        FadeTo(CompactView, 0, TimeSpan.FromMilliseconds(90), () => CompactView.Visibility = Visibility.Collapsed);
        ExpandedView.Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            BeginTime = TimeSpan.FromMilliseconds(190),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ExpandedView.BeginAnimation(OpacityProperty, fadeIn);
        PlaySheen();

        InstallMouseHook();
    }

    private void Collapse()
    {
        RemoveMouseHook();
        SetNoActivate(true);
        if (_preExpandTop is { } restore)
        {
            _preExpandTop = null;
            Top = restore;
        }

        // release the expanded panel's garbage once the animation settles
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        t.Tick += (_, _) => { t.Stop(); TrimWorkingSet(); };
        t.Start();

        AnimateIsland(toWidth: CompactWidth, toHeight: CompactHeight, toRadius: CompactRadius,
            duration: TimeSpan.FromMilliseconds(180),
            easing: new CubicEase { EasingMode = EasingMode.EaseIn });

        CompactView.Visibility = Visibility.Visible;
        FadeTo(ExpandedView, 0, TimeSpan.FromMilliseconds(90), () => ExpandedView.Visibility = Visibility.Collapsed);
        CompactView.Opacity = 0;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
        {
            BeginTime = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        CompactView.BeginAnimation(OpacityProperty, fadeIn);
    }

    private void AnimateIsland(double toWidth, double toHeight, double toRadius, TimeSpan duration, IEasingFunction easing)
    {
        _morphing = true;
        var widthAnim = new DoubleAnimation(toWidth, duration) { EasingFunction = easing };
        widthAnim.Completed += (_, _) =>
        {
            _morphing = false;
            CaptureGlass();
        };
        var heightAnim = new DoubleAnimation(toHeight, duration) { EasingFunction = easing };
        var radiusAnim = new CornerRadiusAnimation
        {
            From = IslandBorder.CornerRadius,
            To = new CornerRadius(toRadius),
            Duration = duration,
            EasingFunction = easing
        };
        IslandBorder.BeginAnimation(WidthProperty, widthAnim);
        IslandBorder.BeginAnimation(HeightProperty, heightAnim);
        IslandBorder.BeginAnimation(System.Windows.Controls.Border.CornerRadiusProperty, radiusAnim);
    }

    private static void FadeTo(UIElement element, double to, TimeSpan duration, Action? completed = null)
    {
        var anim = new DoubleAnimation(to, duration);
        if (completed is not null)
            anim.Completed += (_, _) => completed();
        element.BeginAnimation(OpacityProperty, anim);
    }

    private void SetNoActivate(bool enabled)
    {
        if (_hwnd == IntPtr.Zero) return;
        int style = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
        style = enabled ? style | NativeMethods.WS_EX_NOACTIVATE : style & ~NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, style);
    }

    // ── Click-outside detection (low-level mouse hook, installed only while expanded) ──

    private void InstallMouseHook()
    {
        if (_mouseHook != IntPtr.Zero) return;
        _mouseProc = MouseHookCallback;
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc,
            NativeMethods.GetModuleHandle(null), 0);
    }

    private void RemoveMouseHook()
    {
        if (_mouseHook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
        _mouseProc = null;
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Zero blocking work here: capture the point, decide on the dispatcher.
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is NativeMethods.WM_LBUTTONDOWN or NativeMethods.WM_RBUTTONDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var screenPoint = new Point(data.pt.X, data.pt.Y);
                Dispatcher.BeginInvoke(() =>
                {
                    if (!_model.IsExpanded) return;
                    // PointToScreen yields device pixels — same space as the hook's point.
                    var topLeft = IslandBorder.PointToScreen(new Point(0, 0));
                    var bottomRight = IslandBorder.PointToScreen(new Point(IslandBorder.ActualWidth, IslandBorder.ActualHeight));
                    var bounds = new Rect(topLeft, bottomRight);
                    if (!bounds.Contains(screenPoint))
                        _model.Collapse();
                });
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }
}
