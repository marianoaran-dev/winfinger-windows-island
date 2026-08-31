using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace WinFinger.Controls;

/// <summary>"Just now / 5 min ago / Yesterday 14:30 / 08-12" style timestamps.</summary>
public sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime time) return "";
        var delta = DateTime.Now - time;
        if (delta.TotalMinutes < 1) return "Just now";
        if (delta.TotalHours < 1) return $"{(int)delta.TotalMinutes} min ago";
        if (time.Date == DateTime.Today) return time.ToString("HH:mm");
        if (time.Date == DateTime.Today.AddDays(-1)) return $"Yesterday {time:HH:mm}";
        return time.ToString("dd-MM");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Loads a decode-limited, frozen thumbnail from an image path (null-safe).</summary>
public sealed class ImagePathToThumbnailConverter : IValueConverter
{
    public int DecodeWidth { get; set; } = 240;

    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || !File.Exists(path)) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = DecodeWidth;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Null/empty → Collapsed.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        => value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
