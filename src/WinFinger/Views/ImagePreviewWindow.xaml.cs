using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WinFinger.Views;

/// <summary>Full-screen lightbox for clipboard screenshots: wheel zooms, drag pans, Esc/click closes.</summary>
public partial class ImagePreviewWindow : Window
{
    private bool _panning;
    private Point _panStart;
    private double _panStartX, _panStartY;
    private bool _moved;

    public ImagePreviewWindow(string imagePath)
    {
        InitializeComponent();
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(imagePath);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        Preview.Source = bmp;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Space) Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        double prev = Zoom.ScaleX;
        double next = Math.Clamp(prev * (e.Delta > 0 ? 1.15 : 1 / 1.15), 1.0, 8.0);
        // keep the pixel under the cursor fixed: screen = center + (local-center)*scale + pan
        var local = e.GetPosition(Preview);
        var center = new Point(Preview.ActualWidth / 2, Preview.ActualHeight / 2);
        Pan.X += (local.X - center.X) * (prev - next);
        Pan.Y += (local.Y - center.Y) * (prev - next);
        Zoom.ScaleX = Zoom.ScaleY = next;
        if (next <= 1.001) { Pan.X = 0; Pan.Y = 0; }
        ZoomLabel.Text = $"{next * 100:0}%";
        e.Handled = true;
    }

    private void OnBackdropDown(object sender, MouseButtonEventArgs e)
    {
        _moved = false;
        if (Zoom.ScaleX > 1.001)
        {
            _panning = true;
            _panStart = e.GetPosition(this);
            _panStartX = Pan.X;
            _panStartY = Pan.Y;
            CaptureMouse();
        }
    }

    private void OnMouseMoveWin(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _panStart.X) > 3 || Math.Abs(p.Y - _panStart.Y) > 3) _moved = true;
        Pan.X = _panStartX + (p.X - _panStart.X);
        Pan.Y = _panStartY + (p.Y - _panStart.Y);
    }

    private void OnBackdropUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning)
        {
            _panning = false;
            ReleaseMouseCapture();
        }
        // plain click (no drag) anywhere closes the lightbox
        if (!_moved) Close();
    }
}
