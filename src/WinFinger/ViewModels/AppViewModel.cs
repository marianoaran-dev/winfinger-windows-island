using CommunityToolkit.Mvvm.ComponentModel;
using WinFinger.Services;

namespace WinFinger.ViewModels;

public enum AppPage
{
    Clipboard,
    Media,
    Notes,
    Shortcuts,
    Pomodoro
}

/// <summary>Central application state (counterpart of mac's AppModel).</summary>
public sealed partial class AppViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private AppPage _selectedPage = AppPage.Clipboard;

    public MetricsService Metrics { get; } = new();
    public ClipboardStore ClipboardStore { get; } = new();
    public ClipboardMonitorService ClipboardMonitor { get; }
    public NoteStore Notes { get; } = new();
    public ShortcutCatalogService ShortcutCatalog { get; } = new();
    public ForegroundAppService ForegroundApp { get; } = new();
    public MediaService Media { get; } = new();
    public AudioVisualizerService Visualizer { get; } = new();
    public PomodoroService Pomodoro { get; } = new();
    public PowerStatusService PowerStatus { get; } = new();
    public NotificationService Notifications { get; } = new();
    public SettingsService SettingsStore { get; } = new();
    public IslandActivityState IslandActivity { get; } = new();

    public AppViewModel()
    {
        ClipboardMonitor = new ClipboardMonitorService(ClipboardStore);

        var settings = SettingsStore.Settings;
        ClipboardMonitor.IsPaused = settings.ClipboardPaused;
        Pomodoro.FocusMinutes = settings.PomodoroFocusMinutes;
        Pomodoro.BreakMinutes = settings.PomodoroBreakMinutes;

        ClipboardMonitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClipboardMonitorService.IsPaused))
            {
                settings.ClipboardPaused = ClipboardMonitor.IsPaused;
                SettingsStore.Save();
            }
        };
        Pomodoro.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PomodoroService.FocusMinutes) or nameof(PomodoroService.BreakMinutes))
            {
                settings.PomodoroFocusMinutes = Pomodoro.FocusMinutes;
                settings.PomodoroBreakMinutes = Pomodoro.BreakMinutes;
                SettingsStore.Save();
            }

            if (e.PropertyName == nameof(PomodoroService.Phase))
                IslandActivity.SetTimerActive(Pomodoro.Phase != PomodoroPhase.Idle);
        };
        Media.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MediaService.IsPlaying))
            {
                if (Media.IsPlaying) Visualizer.Start();
                else Visualizer.Stop();
            }

            if (e.PropertyName == nameof(MediaService.HasSession))
                IslandActivity.SetMediaAvailable(Media.HasSession);
        };
        PowerStatus.StatusChanged += change =>
        {
            var previous = change.Previous;
            var current = change.Current;

            bool supplyChanged = previous.PowerSupplyStatus != current.PowerSupplyStatus;
            bool batteryStateChanged = previous.BatteryStatus != current.BatteryStatus;
            bool crossedLowThreshold =
                previous.RemainingChargePercent > 20 && current.RemainingChargePercent <= 20 ||
                previous.RemainingChargePercent > 10 && current.RemainingChargePercent <= 10;

            if (!supplyChanged && !batteryStateChanged && !crossedLowThreshold)
                return;

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                string icon;
                string message;

                if (current.IsCharging)
                {
                    icon = "⚡";
                    message = $"Charging · {current.RemainingChargePercent}%";
                }
                else if (current.BatteryStatus == Windows.System.Power.BatteryStatus.Discharging)
                {
                    icon = current.RemainingChargePercent <= 20 ? "🪫" : "🔋";
                    message = $"Battery · {current.RemainingChargePercent}%";
                }
                else
                {
                    icon = "🔋";
                    message = $"Battery · {current.RemainingChargePercent}%";
                }

                Notifications.Post(icon, message);
            });
        };
        Pomodoro.PhaseCompleted += phase =>
        {
            Notifications.Post("🍅", phase == PomodoroPhase.Focus ? "Focus complete. Time for a break." : "Break complete. Time to focus.");
            System.Media.SystemSounds.Asterisk.Play();
        };
    }

    public void Start()
    {
        StoragePaths.EnsureCreated();
        Metrics.Start();
        ForegroundApp.Start();
        Media.Start();
        PowerStatus.Start();
    }

    public void Stop()
    {
        PowerStatus.Stop();
        Metrics.Stop();
        ForegroundApp.Stop();
        ClipboardMonitor.Detach();
        Visualizer.Stop();
        Media.Stop();
        Pomodoro.Pause();
    }

    public void ToggleExpanded()
    {
        IslandActivity.ToggleExpanded();
        IsExpanded = IslandActivity.IsExpanded;
    }

    public void Collapse()
    {
        IslandActivity.Collapse();
        IsExpanded = false;
    }

    public void Select(AppPage page)
    {
        SelectedPage = page;
        IsExpanded = true;
    }
}
