using CommunityToolkit.Mvvm.ComponentModel;
using WinFinger.Models;

namespace WinFinger.ViewModels;

public enum IslandActivityKind
{
    Idle,
    Media,
    Timer,
    Notification,
    Clipboard
}

public readonly record struct IslandGeometry(double Width, double Height, double CornerRadius)
{
    public static readonly IslandGeometry Idle = new(184, 34, 17);
    public static readonly IslandGeometry MediaCompact = new(320, 64, 24);
    public static readonly IslandGeometry MediaExpanded = new(430, 210, 30);
    public static readonly IslandGeometry TimerCompact = new(300, 64, 24);
    public static readonly IslandGeometry TimerExpanded = new(380, 180, 28);
    public static readonly IslandGeometry Notification = new(360, 52, 22);
    public static readonly IslandGeometry ClipboardText = new(380, 82, 24);
    public static readonly IslandGeometry ClipboardImage = new(400, 174, 28);
    public static readonly IslandGeometry ClipboardImageExpanded = new(440, 300, 30);
}

/// <summary>
/// Small presentation coordinator for the DynamicNotch-style shell.
/// It intentionally owns presentation only; Windows media, timer, clipboard and
/// other services remain independent and feed activity state into this coordinator.
/// </summary>
public sealed partial class IslandActivityState : ObservableObject
{
    [ObservableProperty] private IslandActivityKind _kind = IslandActivityKind.Idle;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private string _notificationIcon = string.Empty;
    [ObservableProperty] private string _notificationText = string.Empty;
    [ObservableProperty] private ClipboardEntry? _clipboardEntry;

    private IslandActivityKind _persistentKind = IslandActivityKind.Idle;
    private IslandActivityKind? _lastDismissedKind;
    private bool _mediaAvailable;
    private bool _timerActive;

    public bool HasTemporaryActivity => Kind is IslandActivityKind.Notification or IslandActivityKind.Clipboard;
    public bool CanExpand => Kind is IslandActivityKind.Media or IslandActivityKind.Timer ||
        (Kind == IslandActivityKind.Clipboard && ClipboardEntry?.Kind == ClipboardEntryKind.Image);
    public bool CanDismiss => Kind != IslandActivityKind.Idle;
    public bool CanRestore => _lastDismissedKind is not null;

    public IslandGeometry Geometry => Kind switch
    {
        IslandActivityKind.Media when IsExpanded => IslandGeometry.MediaExpanded,
        IslandActivityKind.Media => IslandGeometry.MediaCompact,
        IslandActivityKind.Timer when IsExpanded => IslandGeometry.TimerExpanded,
        IslandActivityKind.Timer => IslandGeometry.TimerCompact,
        IslandActivityKind.Notification => IslandGeometry.Notification,
        IslandActivityKind.Clipboard when IsExpanded && ClipboardEntry?.Kind == ClipboardEntryKind.Image => IslandGeometry.ClipboardImageExpanded,
        IslandActivityKind.Clipboard when ClipboardEntry?.Kind == ClipboardEntryKind.Image => IslandGeometry.ClipboardImage,
        IslandActivityKind.Clipboard => IslandGeometry.ClipboardText,
        _ => IslandGeometry.Idle
    };

    public void SetMediaAvailable(bool available)
    {
        _mediaAvailable = available;
        SyncPersistentActivity();
    }

    public void SetTimerActive(bool active)
    {
        _timerActive = active;
        SyncPersistentActivity();
    }

    public void ShowTemporaryNotification(string icon, string text)
    {
        ClipboardEntry = null;
        NotificationIcon = icon;
        NotificationText = text;
        IsExpanded = false;
        SetKind(IslandActivityKind.Notification);
    }

    public void ShowTemporaryClipboard(ClipboardEntry entry)
    {
        NotificationIcon = string.Empty;
        NotificationText = string.Empty;
        ClipboardEntry = entry;
        IsExpanded = false;
        SetKind(IslandActivityKind.Clipboard);
    }

    public void HideTemporaryActivity()
    {
        if (!HasTemporaryActivity) return;
        IsExpanded = false;
        SetKind(_persistentKind);
    }

    public void HideTemporaryNotification() => HideTemporaryActivity();

    public void ToggleExpanded()
    {
        if (!CanExpand) return;
        IsExpanded = !IsExpanded;
        OnPropertyChanged(nameof(Geometry));
    }

    public void Collapse()
    {
        if (!IsExpanded) return;
        IsExpanded = false;
        OnPropertyChanged(nameof(Geometry));
    }

    public void DismissCurrent()
    {
        if (!CanDismiss) return;

        // Temporary clipboard/notification previews dismiss back to the persistent
        // activity, but do not replace the user's restore history.
        if (HasTemporaryActivity)
        {
            IsExpanded = false;
            SetKind(_persistentKind);
            return;
        }

        _lastDismissedKind = Kind;
        IsExpanded = false;
        _persistentKind = IslandActivityKind.Idle;
        SetKind(IslandActivityKind.Idle);
        OnPropertyChanged(nameof(CanRestore));
    }

    public void RestoreLastDismissed()
    {
        if (_lastDismissedKind is not { } restore) return;
        _lastDismissedKind = null;
        _persistentKind = restore;
        SetKind(restore);
        OnPropertyChanged(nameof(CanRestore));
    }

    private void SyncPersistentActivity()
    {
        _persistentKind = _timerActive
            ? IslandActivityKind.Timer
            : _mediaAvailable
                ? IslandActivityKind.Media
                : IslandActivityKind.Idle;

        if (!HasTemporaryActivity)
        {
            IsExpanded = false;
            SetKind(_persistentKind);
        }
    }

    private void SetKind(IslandActivityKind kind)
    {
        if (Kind == kind)
        {
            OnPropertyChanged(nameof(Geometry));
            OnPropertyChanged(nameof(CanExpand));
            return;
        }

        Kind = kind;
        OnPropertyChanged(nameof(Geometry));
        OnPropertyChanged(nameof(HasTemporaryActivity));
        OnPropertyChanged(nameof(CanExpand));
        OnPropertyChanged(nameof(CanDismiss));
        OnPropertyChanged(nameof(CanRestore));
    }
}