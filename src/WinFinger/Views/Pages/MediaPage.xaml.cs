using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WinFinger.Services;
using WinFinger.ViewModels;

namespace WinFinger.Views.Pages;

public partial class MediaPage : UserControl, IIslandPage
{
    private AppViewModel? _model;

    public MediaPage()
    {
        InitializeComponent();
    }

    public void Initialize(AppViewModel model)
    {
        _model = model;
        PlayPauseButton.Click += (_, _) => model.Media.TogglePlayPause();
        NextButton.Click += (_, _) => model.Media.Next();
        PrevButton.Click += (_, _) => model.Media.Previous();
        model.Media.PropertyChanged += OnMediaChanged;
        Refresh();
    }

    public void OnShown() => Refresh();

    private void OnMediaChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_model is null) return;
        var media = _model.Media;

        bool showPlayer = media.HasSession && (media.Title.Length > 0 || media.Cover is not null);
        PlayerPane.Visibility = showPlayer ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = showPlayer ? Visibility.Collapsed : Visibility.Visible;
        if (!showPlayer) return;

        TitleLabel.Text = media.Title.Length > 0 ? media.Title : "Unknown track";
        ArtistLabel.Text = media.Artist;
        CoverImage.Source = media.Cover;
        CoverPlaceholder.Visibility = media.Cover is null ? Visibility.Visible : Visibility.Collapsed;
        PlayPauseGlyph.Text = media.IsPlaying ? "" : "";
    }
}
