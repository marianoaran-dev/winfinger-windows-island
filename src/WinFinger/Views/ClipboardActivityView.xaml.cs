using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using WinFinger.Models;

namespace WinFinger.Views;

public partial class ClipboardActivityView : UserControl
{
    private ClipboardEntry? _entry;

    public ClipboardActivityView()
    {
        InitializeComponent();
    }

    public void SetEntry(ClipboardEntry? entry)
    {
        _entry = entry;
        ImagePreview.Source = null;
        ExpandedImagePreview.Source = null;
        ImagePreview.Visibility = Visibility.Collapsed;
        ClipboardGlyph.Visibility = Visibility.Visible;
        PreviewText.Visibility = Visibility.Visible;
        ExpandedImageFallback.Visibility = Visibility.Visible;

        if (entry is null)
        {
            HeadingText.Text = "Copied";
            PreviewText.Text = string.Empty;
            SourceText.Text = string.Empty;
            ExpandedSourceText.Text = string.Empty;
            return;
        }

        string source = string.IsNullOrWhiteSpace(entry.SourceAppName)
            ? "Clipboard"
            : $"Copied from {entry.SourceAppName}";
        SourceText.Text = source;
        ExpandedSourceText.Text = source;

        if (entry.Kind == ClipboardEntryKind.Image)
        {
            HeadingText.Text = "Screenshot copied";
            PreviewText.Text = "Click to focus preview";

            if (TryLoadBitmap(entry.ImagePath, out var bitmap))
            {
                ImagePreview.Source = bitmap;
                ImagePreview.Visibility = Visibility.Visible;
                ClipboardGlyph.Visibility = Visibility.Collapsed;
                ExpandedImagePreview.Source = bitmap;
                ExpandedImageFallback.Visibility = Visibility.Collapsed;
            }
            return;
        }

        HeadingText.Text = "Copied";
        PreviewText.Text = NormalisePreview(entry.Text);
    }

    public void SetExpanded(bool expanded, bool animate)
    {
        if (_entry?.Kind != ClipboardEntryKind.Image)
            expanded = false;

        if (!animate)
        {
            CompactLayout.BeginAnimation(OpacityProperty, null);
            ExpandedLayout.BeginAnimation(OpacityProperty, null);
            CompactLayout.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
            ExpandedLayout.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            CompactLayout.Opacity = expanded ? 0 : 1;
            ExpandedLayout.Opacity = expanded ? 1 : 0;
            return;
        }

        var incoming = expanded ? ExpandedLayout : CompactLayout;
        var outgoing = expanded ? CompactLayout : ExpandedLayout;
        incoming.Visibility = Visibility.Visible;
        incoming.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190))
            {
                BeginTime = TimeSpan.FromMilliseconds(90),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });

        if (outgoing.Visibility == Visibility.Visible)
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(90));
            fade.Completed += (_, _) => outgoing.Visibility = Visibility.Collapsed;
            outgoing.BeginAnimation(OpacityProperty, fade);
        }
    }

    private static bool TryLoadBitmap(string? path, out BitmapImage? bitmap)
    {
        bitmap = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var loaded = new BitmapImage();
            loaded.BeginInit();
            loaded.CacheOption = BitmapCacheOption.OnLoad;
            loaded.UriSource = new Uri(path, UriKind.Absolute);
            loaded.EndInit();
            loaded.Freeze();
            bitmap = loaded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalisePreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Text added to clipboard history";

        var single = text.ReplaceLineEndings(" ").Trim();
        const int max = 130;
        return single.Length <= max ? single : single[..max] + "…";
    }
}