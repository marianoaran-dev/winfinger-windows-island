using CommunityToolkit.Mvvm.ComponentModel;

namespace WinFinger.ViewModels;

public enum IslandActivityKind
{
    Idle,
    Media,
    Notification
}

public readonly record struct IslandGeometry(double Width, double Height, double CornerRadius)
{
    public static readonly IslandGeometry Idle = new(184, 34, 17);
    public static readonly IslandGeometry MediaCompact = new(320, 64, 24);
    public static readonly IslandGeometry MediaExpanded = new(430, 210, 30);
    public static readonly IslandGeometry Notification = new(360, 52, 22);
}

/// <summary>
/// Small presentation coordinator for the DynamicNotch-style shell.
/// It intentionally owns presentation only; Windows media, clipboard and other
/// services remain independent and feed activity state into this coordinator.
/// </summary>
public sealed partial class IslandActivityState : ObservableObject
{
    [ObservableProperty] private IslandActivityKind _kind = IslandActivityKind.Idle;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private string _notificationIcon = string.Empty;
    [ObservableProperty] private string _notificationText = string.Empty;

    private IslandActivityKind _persistentKind = IslandActivityKind.Idle;
    private IslandActivityKind? _lastDismissedKind;

    public bool HasTemporaryActivity => Kind == IslandActivityKind.Notification;
    public bool CanExpand => Kind == IslandActivityKind.Media;
    public bool CanDismiss => Kind != IslandActivityKind.Idle;
    public bool CanRestore => _lastDismissedKind is not null;

    public IslandGeometry Geometry => Kind switch
    {
        IslandActivityKind.Media when IsExpanded => IslandGeometry.MediaExpanded,
        IslandActivityKind.Media => IslandGeometry.MediaCompact,
        IslandActivityKind.Notification => IslandGeometry.Notification,
        _ => IslandGeometry.Idle
    };

    public void SetMediaAvailable(bool available)
    {
        _persistentKind = available ? IslandActivityKind.Media : IslandActivityKind.Idle;
        if (!HasTemporaryActivity)
            SetKind(_persistentKind);
    }

    public void ShowTemporaryNotification(string icon, string text)
    {
        NotificationIcon = icon;
        NotificationText = text;
        IsExpanded = false;
        SetKind(IslandActivityKind.Notification);
    }

    public void HideTemporaryNotification()
    {
        if (!HasTemporaryActivity) return;
        SetKind(_persistentKind);
    }

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
        _lastDismissedKind = Kind;
        IsExpanded = false;

        if (HasTemporaryActivity)
        {
            SetKind(_persistentKind);
            return;
        }

        _persistentKind = IslandActivityKind.Idle;
        SetKind(IslandActivityKind.Idle);
        OnPropertyChanged(nameof(CanRestore));
    }

    public void RestoreLastDismissed()
    {
        if (_lastDismissedKind is not { } restore) return;
        _lastDismissedKind = null;

        if (restore == IslandActivityKind.Media)
            _persistentKind = IslandActivityKind.Media;

        SetKind(restore == IslandActivityKind.Notification ? _persistentKind : restore);
        OnPropertyChanged(nameof(CanRestore));
    }

    private void SetKind(IslandActivityKind kind)
    {
        if (Kind == kind)
        {
            OnPropertyChanged(nameof(Geometry));
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
