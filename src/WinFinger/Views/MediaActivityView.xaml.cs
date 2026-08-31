using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using WinFinger.Services;
using WinFinger.ViewModels;

namespace WinFinger.Views;

public partial class MediaActivityView : UserControl
{
    private AppViewModel? _model;
    private bool _expanded;

    public MediaActivityView()
    {
        InitializeComponent();
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
            Refresh();
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
