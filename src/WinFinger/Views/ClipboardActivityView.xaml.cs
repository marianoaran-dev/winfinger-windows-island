using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WinFinger.Models;

namespace WinFinger.Views;

public partial class ClipboardActivityView : UserControl
{
    public ClipboardActivityView()
    {
        InitializeComponent();
    }

    public void SetEntry(ClipboardEntry? entry)
    {
        ImagePreview.Source = null;
        ImagePreview.Visibility = Visibility.Collapsed;
        ClipboardGlyph.Visibility = Visibility.Visible;
        PreviewText.Visibility = Visibility.Visible;

        if (entry is null)
        {
            HeadingText.Text = "Copied";
            PreviewText.Text = string.Empty;
            SourceText.Text = string.Empty;
            return;
        }

        SourceText.Text = string.IsNullOrWhiteSpace(entry.SourceAppName)
            ? "Clipboard"
            : $"Copied from {entry.SourceAppName}";

        if (entry.Kind == ClipboardEntryKind.Image)
        {
            HeadingText.Text = "Screenshot copied";
            PreviewText.Text = "Image added to clipboard history";

            if (!string.IsNullOrWhiteSpace(entry.ImagePath) && File.Exists(entry.ImagePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(entry.ImagePath, UriKind.Absolute);
                    bitmap.EndInit();
                    bitmap.Freeze();
                    ImagePreview.Source = bitmap;
                    ImagePreview.Visibility = Visibility.Visible;
                    ClipboardGlyph.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    // Keep the resilient text fallback if an image file is stale or unreadable.
                }
            }
            return;
        }

        HeadingText.Text = "Copied";
        PreviewText.Text = NormalisePreview(entry.Text);
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