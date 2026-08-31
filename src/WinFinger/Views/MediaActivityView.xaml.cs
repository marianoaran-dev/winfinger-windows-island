using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using WinFinger.Services;
using WinFinger.ViewModels;

namespace WinFinger.Views;

public partial class MediaActivityView : UserControl
{
    private AppViewModel? _model;
    private bool _expanded;
    private bool _isSeeking;
    private double _seekPreviewFraction;

    public MediaActivityView()
    {
        InitializeComponent();
        SeekRegion.SizeChanged += (_, _) => RefreshTimelineVisual();
    }

    public void Initialize(AppViewModel model)
    {
        _model = model;
        model.Media.PropertyChanged += OnMediaPropertyChanged;
        Refresh();
    }

    public void SetExpanded(bool expanded, bool animate = true)
    {
        if (_expanded == expanded) return;
        _expanded = expanded;

        var outgoing = expanded ? CompactLayout : ExpandedLayout;
        var incoming = expanded ? ExpandedLayout : CompactLayout;

        if (!animate)
        {
            outgoing.Visibility = Visibility.Collapsed;
            outgoing.Opacity = 0;
            incoming.Visibility = Visibility.Visible;
            incoming.Opacity = 1;
            RefreshTimelineVisual();
            return;
        }

        incoming.Visibility = Visibility.Visible;
        incoming.Opacity = 0;
        outgoing.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(90)));
        incoming.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
            {
                BeginTime = TimeSpan.FromMilliseconds(90),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        var hideTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            outgoing.Visibility = Visibility.Collapsed;
            RefreshTimelineVisual();
        };
        hideTimer.Start();
    }

    private void OnMediaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MediaService.Title)
            or nameof(MediaService.Artist)
            or nameof(MediaService.Cover)
            or nameof(MediaService.IsPlaying)
            or nameof(MediaService.HasSession))
        {
            Refresh();
            return;
        }

        if (e.PropertyName is nameof(MediaService.Position)
            or nameof(MediaService.Duration)
            or nameof(MediaService.Progress)
            or nameof(MediaService.CanSeek))
        {
            RefreshTimelineVisual();
        }
    }

    private void Refresh()
    {
        if (_model is null) return;
        var media = _model.Media;

        string title = string.IsNullOrWhiteSpace(media.Title) ? "Now Playing" : media.Title;
        string artist = string.IsNullOrWhiteSpace(media.Artist) ? "Windows media" : media.Artist;
        CompactTitle.Text = title;
        ExpandedTitle.Text = title;
        CompactArtist.Text = artist;
        ExpandedArtist.Text = artist;
        CompactCover.Source = media.Cover;
        ExpandedCover.Source = media.Cover;
        bool hasCover = media.Cover is not null;
        CompactCoverFallback.Visibility = hasCover ? Visibility.Collapsed : Visibility.Visible;
        ExpandedCoverFallback.Visibility = hasCover ? Visibility.Collapsed : Visibility.Visible;

        string glyph = media.IsPlaying ? "Ⅱ" : "▶";
        PlaybackGlyph.Text = glyph;
        PlayPauseButton.Content = glyph;
        RefreshTimelineVisual();
    }

    private void RefreshTimelineVisual()
    {
        if (_model is null || SeekRegion.ActualWidth <= 0) return;
        var media = _model.Media;
        double fraction = _isSeeking ? _seekPreviewFraction : Math.Clamp(media.Progress, 0, 1);
        double width = Math.Max(0, SeekRegion.ActualWidth);

        SeekProgress.Width = width * fraction;
        SeekThumb.Margin = new Thickness(width * fraction - 4, 0, 0, 0);
        SeekRegion.Opacity = media.CanSeek ? 1 : 0.45;

        var shownPosition = _isSeeking && media.Duration > TimeSpan.Zero
            ? TimeSpan.FromTicks((long)(media.Duration.Ticks * fraction))
            : media.Position;
        PositionText.Text = FormatTime(shownPosition);
        DurationText.Text = FormatTime(media.Duration);
    }

    private void OnSeekMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_model?.Media.CanSeek != true) return;
        e.Handled = true;
        _isSeeking = true;
        SeekRegion.CaptureMouse();
        UpdateSeekPreview(e.GetPosition(SeekRegion).X);
    }

    private void OnSeekMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isSeeking) return;
        e.Handled = true;
        UpdateSeekPreview(e.GetPosition(SeekRegion).X);
    }

    private void OnSeekMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSeeking) return;
        e.Handled = true;
        UpdateSeekPreview(e.GetPosition(SeekRegion).X);
        _isSeeking = false;
        SeekRegion.ReleaseMouseCapture();
        _model?.Media.SeekToFraction(_seekPreviewFraction);
        RefreshTimelineVisual();
    }

    private void UpdateSeekPreview(double x)
    {
        double width = Math.Max(1, SeekRegion.ActualWidth);
        _seekPreviewFraction = Math.Clamp(x / width, 0, 1);
        RefreshTimelineVisual();
    }

    private static string FormatTime(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{(int)value.TotalMinutes}:{value.Seconds:00}";
    }

    private void OnPrevious(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _model?.Media.Previous();
    }

    private void OnPlayPause(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _model?.Media.TogglePlayPause();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _model?.Media.Next();
    }
}
