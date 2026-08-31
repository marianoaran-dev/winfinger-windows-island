using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WinFinger.Controls;
using WinFinger.Models;
using WinFinger.ViewModels;

namespace WinFinger.Views;

/// <summary>
/// DynamicNotch-fidelity presentation layer for <see cref="IslandWindow"/>.
/// The legacy window keeps owning Windows integration, positioning and fallback
/// pages while this partial class owns the content-driven activity shell.
/// </summary>
public partial class IslandWindow
{
    private readonly DispatcherTimer _activityNotificationTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(2600)
    };

    private bool _dynamicNotchAttached;
    private bool _dynamicRenderPending;
    private bool _legacyPanelActive;
    private ScaleTransform? _pressScale;

    private void OnDynamicNotchLoaded(object sender, RoutedEventArgs e)
    {
        if (_dynamicNotchAttached) return;
        _dynamicNotchAttached = true;

        MediaActivityView.Initialize(_model);
        TimerActivityView.Initialize(_model);

        IslandBorder.MouseLeftButtonUp -= OnIslandClicked;
        IslandBorder.MouseEnter -= OnIslandMouseEnter;
        IslandBorder.MouseLeave -= OnIslandMouseLeave;
        IslandBorder.MouseLeftButtonUp += OnDynamicIslandMouseUp;
        IslandBorder.MouseLeftButtonDown += OnDynamicIslandPress;
        IslandBorder.MouseLeave += OnDynamicIslandMouseLeave;

        _model.PropertyChanged -= OnModelPropertyChanged;
        _model.PropertyChanged += OnDynamicModelPropertyChanged;

        _model.Notifications.NotificationPosted -= OnNotificationPosted;
        _model.Notifications.NotificationPosted += OnDynamicNotificationPosted;
        _model.ClipboardStore.Entries.CollectionChanged += OnDynamicClipboardChanged;
        _activityNotificationTimer.Tick += (_, _) =>
        {
            _activityNotificationTimer.Stop();
            _model.IslandActivity.HideTemporaryActivity();
        };

        _model.IslandActivity.PropertyChanged += OnDynamicActivityChanged;

        _pressScale = new ScaleTransform(1, 1);
        IslandBorder.RenderTransformOrigin = new Point(0.5, 0);
        IslandBorder.RenderTransform = _pressScale;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            ApplyDynamicNotchSurface();
            RenderDynamicActivity(animate: false);
            ApplyDynamicDemoStateFromEnvironment();
        }));
    }

    private void ApplyDynamicNotchSurface()
    {
        _glassTimer?.Stop();
        _model.Media.PropertyChanged -= OnMediaChangedForGlow;

        GlassLayer.Background = Brushes.Black;
        ImageDimLayer.Opacity = 0;
        BodyTintLayer.Opacity = 0;
        TintLayer.Opacity = 0;
        ChromaticLayer.Visibility = Visibility.Collapsed;
        GlintA.Visibility = Visibility.Collapsed;
        GlintB.Visibility = Visibility.Collapsed;
        SheenBand.Visibility = Visibility.Collapsed;

        IslandBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255));
        IslandBorder.BorderThickness = new Thickness(1);
        IslandShadow.Color = Colors.Black;
        IslandShadow.Opacity = 0.42;
        IslandShadow.BlurRadius = 20;
        IslandShadow.ShadowDepth = 5;
    }

    private void ApplyDynamicDemoStateFromEnvironment()
    {
        string? demo = Environment.GetEnvironmentVariable("WINFINGER_DYNAMICNOTCH_DEMO")?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(demo)) return;

        switch (demo)
        {
            case "idle":
                _model.Pomodoro.Reset();
                _model.IslandActivity.SetMediaAvailable(false);
                break;
            case "media-compact":
                _model.Pomodoro.Reset();
                _model.IslandActivity.SetMediaAvailable(true);
                _model.IslandActivity.Collapse();
                break;
            case "media-expanded":
                _model.Pomodoro.Reset();
                _model.IslandActivity.SetMediaAvailable(true);
                if (!_model.IslandActivity.IsExpanded)
                    _model.IslandActivity.ToggleExpanded();
                break;
            case "timer-compact":
            case "timer-expanded":
                _model.IslandActivity.SetMediaAvailable(true);
                _model.Pomodoro.StartFocus();
                _model.Pomodoro.Pause();
                _model.IslandActivity.Collapse();
                if (demo == "timer-expanded")
                    _model.IslandActivity.ToggleExpanded();
                break;
            case "notification":
                _model.IslandActivity.SetMediaAvailable(true);
                _model.IslandActivity.ShowTemporaryNotification("♪", "Volume 42%");
                break;
            case "clipboard-text":
                _model.IslandActivity.SetMediaAvailable(true);
                _model.IslandActivity.ShowTemporaryClipboard(new ClipboardEntry(
                    Guid.NewGuid(), ClipboardEntryKind.Text,
                    "Dynamic clipboard preview with content-driven geometry.", null,
                    null, "Demo app", DateTime.Now, "demo-text"));
                break;
            case "clipboard-image":
            case "clipboard-image-expanded":
                _model.IslandActivity.SetMediaAvailable(true);
                _model.IslandActivity.ShowTemporaryClipboard(new ClipboardEntry(
                    Guid.NewGuid(), ClipboardEntryKind.Image, null, null,
                    null, "Snipping Tool", DateTime.Now, "demo-image"));
                if (demo == "clipboard-image-expanded")
                    _model.IslandActivity.ToggleExpanded();
                break;
        }
    }

    private void OnDynamicActivityChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_dynamicRenderPending) return;
        _dynamicRenderPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _dynamicRenderPending = false;
            RenderDynamicActivity(animate: true);
        }));
    }

    private void RenderDynamicActivity(bool animate)
    {
        if (_legacyPanelActive) return;

        var activity = _model.IslandActivity;
        var geometry = activity.Geometry;

        if (animate)
        {
            double response = activity.IsExpanded ? 0.45 : 0.47;
            double durationSeconds = activity.IsExpanded ? 0.68 : 0.72;
            AnimateIsland(
                geometry.Width,
                geometry.Height,
                geometry.CornerRadius,
                TimeSpan.FromSeconds(durationSeconds),
                new DampedSpringEase
                {
                    EasingMode = EasingMode.EaseIn,
                    ResponseSeconds = response,
                    DampingFraction = 0.75,
                    DurationSeconds = durationSeconds
                });
        }
        else
        {
            IslandBorder.BeginAnimation(WidthProperty, null);
            IslandBorder.BeginAnimation(HeightProperty, null);
            IslandBorder.BeginAnimation(System.Windows.Controls.Border.CornerRadiusProperty, null);
            IslandBorder.Width = geometry.Width;
            IslandBorder.Height = geometry.Height;
            IslandBorder.CornerRadius = new CornerRadius(geometry.CornerRadius);
        }

        bool showMedia = activity.Kind == IslandActivityKind.Media;
        bool showTimer = activity.Kind == IslandActivityKind.Timer;
        bool showNotification = activity.Kind == IslandActivityKind.Notification;
        bool showClipboard = activity.Kind == IslandActivityKind.Clipboard;

        SetActivityVisibility(MediaActivityView, showMedia, animate);
        SetActivityVisibility(TimerActivityView, showTimer, animate);
        SetActivityVisibility(NotificationView, showNotification, animate);
        SetActivityVisibility(ClipboardActivityView, showClipboard, animate);
        SetActivityVisibility(CompactView, false, animate);
        ExpandedView.Visibility = Visibility.Collapsed;
        ExpandedView.Opacity = 0;

        if (showMedia)
            MediaActivityView.SetExpanded(activity.IsExpanded, animate);
        if (showTimer)
            TimerActivityView.SetExpanded(activity.IsExpanded, animate);

        if (showNotification)
        {
            NotificationIcon.Text = activity.NotificationIcon;
            NotificationText.Text = activity.NotificationText;
        }

        if (showClipboard)
        {
            ClipboardActivityView.SetEntry(activity.ClipboardEntry);
            ClipboardActivityView.SetExpanded(activity.IsExpanded, animate);
        }

        bool shouldActivate = activity.IsExpanded &&
            (showMedia || showTimer || (showClipboard && activity.ClipboardEntry?.Kind == ClipboardEntryKind.Image));
        if (_model.IsExpanded != shouldActivate)
            _model.IsExpanded = shouldActivate;

        if (shouldActivate)
        {
            SetNoActivate(false);
            Activate();
            Focus();
            InstallMouseHook();
        }
        else
        {
            RemoveMouseHook();
            SetNoActivate(true);
        }

        UpdateDynamicShadow(activity);
    }

    private static void SetActivityVisibility(UIElement element, bool visible, bool animate)
    {
        if (!animate)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            element.Opacity = visible ? 1 : 0;
            return;
        }

        if (visible)
        {
            element.Visibility = Visibility.Visible;
            element.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190))
                {
                    BeginTime = TimeSpan.FromMilliseconds(85),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }
        else if (element.Visibility == Visibility.Visible)
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            fade.Completed += (_, _) => element.Visibility = Visibility.Collapsed;
            element.BeginAnimation(OpacityProperty, fade);
        }
    }

    private void UpdateDynamicShadow(IslandActivityState activity)
    {
        double target = activity.Geometry.Height > IslandGeometry.MediaCompact.Height ? 0.48 : 0.18;
        IslandShadow.BeginAnimation(
            System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(260)));
    }

    private void OnDynamicIslandPress(object sender, MouseButtonEventArgs e)
    {
        if (_pressScale is null || e.ChangedButton != MouseButton.Left) return;
        AnimatePressScale(0.985, 0.955, 75, new CubicEase { EasingMode = EasingMode.EaseOut });
    }

    private void OnDynamicIslandMouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
            ReleasePressScale();
    }

    private void OnDynamicIslandMouseUp(object sender, MouseButtonEventArgs e)
    {
        ReleasePressScale();

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
                return;
            }
        }

        var activity = _model.IslandActivity;
        if (activity.Kind is IslandActivityKind.Media or IslandActivityKind.Timer)
        {
            activity.ToggleExpanded();
            e.Handled = true;
        }
        else if (activity.Kind == IslandActivityKind.Clipboard)
        {
            if (activity.ClipboardEntry?.Kind == ClipboardEntryKind.Image)
            {
                _activityNotificationTimer.Stop();
                activity.ToggleExpanded();
                if (!activity.IsExpanded)
                    _activityNotificationTimer.Start();
            }
            else
            {
                _activityNotificationTimer.Stop();
                activity.HideTemporaryActivity();
                _model.Select(AppPage.Clipboard);
            }
            e.Handled = true;
        }
        else if (activity.Kind == IslandActivityKind.Idle && activity.CanRestore)
        {
            activity.RestoreLastDismissed();
            e.Handled = true;
        }
    }

    private void AnimatePressScale(double x, double y, int milliseconds, IEasingFunction easing)
    {
        if (_pressScale is null) return;
        _pressScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
        _pressScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(y, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = easing });
    }

    private void ReleasePressScale()
    {
        const double duration = 0.42;
        var spring = new DampedSpringEase
        {
            EasingMode = EasingMode.EaseIn,
            ResponseSeconds = 0.41,
            DampingFraction = 0.75,
            DurationSeconds = duration
        };
        AnimatePressScale(1, 1, (int)(duration * 1000), spring);
    }

    private void OnDynamicNotificationPosted(Services.IslandNotification notification)
    {
        _activityNotificationTimer.Stop();
        _model.IslandActivity.ShowTemporaryNotification(notification.Icon, notification.Message);
        _activityNotificationTimer.Start();
    }

    private void OnDynamicClipboardChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _legacyPanelActive)
            return;

        var entry = _model.ClipboardStore.Entries.FirstOrDefault();
        if (entry is null) return;

        _activityNotificationTimer.Stop();
        _model.IslandActivity.ShowTemporaryClipboard(entry);
        _activityNotificationTimer.Start();
    }

    private void OnDynamicModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppViewModel.IsExpanded)) return;

        if (_model.IsExpanded)
        {
            if (_model.IslandActivity.IsExpanded) return;
            _legacyPanelActive = true;
            Expand();
        }
        else if (_legacyPanelActive)
        {
            _legacyPanelActive = false;
            Collapse();
            RenderDynamicActivity(animate: true);
        }
    }
}