using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Media.Control;

namespace WinFinger.Services;

/// <summary>Global media session (GSMTC): now-playing info, cover art, timeline, seeking and transport controls.</summary>
public sealed partial class MediaService : ObservableObject
{
    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _artist = "";
    [ObservableProperty] private BitmapImage? _cover;
    [ObservableProperty] private System.Windows.Media.Color _accentColor = System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x34);
    [ObservableProperty] private TimeSpan _position;
    [ObservableProperty] private TimeSpan _duration;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _canSeek;

    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private readonly DispatcherTimer _timelineTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(500)
    };

    public MediaService()
    {
        _timelineTimer.Tick += (_, _) => RefreshTimeline();
    }

    public async void Start()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => OnUi(() => AttachSession(_manager.GetCurrentSession()));
            AttachSession(_manager.GetCurrentSession());
        }
        catch
        {
            // GSMTC unavailable (very old Win10); media page stays empty
        }
    }

    public void Stop()
    {
        _timelineTimer.Stop();
        DetachSession();
        _manager = null;
    }

    public async void TogglePlayPause()
    {
        try
        {
            if (_session is null) return;
            if (IsPlaying) await _session.TryPauseAsync();
            else await _session.TryPlayAsync();
        }
        catch
        {
            // session vanished mid-call
        }
    }

    public async void Next()
    {
        try
        {
            if (_session is not null) await _session.TrySkipNextAsync();
        }
        catch
        {
        }
    }

    public async void Previous()
    {
        try
        {
            if (_session is not null) await _session.TrySkipPreviousAsync();
        }
        catch
        {
        }
    }

    public async void SeekToFraction(double fraction)
    {
        try
        {
            if (_session is null || !CanSeek || Duration <= TimeSpan.Zero) return;
            fraction = Math.Clamp(fraction, 0, 1);
            long ticks = (long)(Duration.Ticks * fraction);
            if (await _session.TryChangePlaybackPositionAsync(ticks))
            {
                Position = TimeSpan.FromTicks(ticks);
                UpdateProgress();
            }
        }
        catch
        {
            // Some players expose a timeline but reject external seeking.
        }
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        DetachSession();
        _session = session;
        if (_session is null)
        {
            HasSession = false;
            IsPlaying = false;
            Title = "";
            Artist = "";
            Cover = null;
            ResetTimeline();
            return;
        }

        HasSession = true;
        _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        RefreshPlayback();
        RefreshTimeline();
        _timelineTimer.Start();
        _ = RefreshPropertiesAsync();
    }

    private void DetachSession()
    {
        _timelineTimer.Stop();
        if (_session is null) return;
        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        _session = null;
    }

    // WinRT events arrive on MTA threads; hop to the UI dispatcher.
    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e)
        => OnUi(() => _ = RefreshPropertiesAsync());

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e)
        => OnUi(() =>
        {
            RefreshPlayback();
            RefreshTimeline();
        });

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e)
        => OnUi(RefreshTimeline);

    private void RefreshPlayback()
    {
        try
        {
            var info = _session?.GetPlaybackInfo();
            IsPlaying = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            IsPlaying = false;
        }
    }

    private void RefreshTimeline()
    {
        try
        {
            if (_session is null)
            {
                ResetTimeline();
                return;
            }

            var timeline = _session.GetTimelineProperties();
            var start = timeline.StartTime;
            var end = timeline.EndTime;
            var position = timeline.Position;
            var duration = end - start;

            if (duration <= TimeSpan.Zero)
            {
                ResetTimeline();
                return;
            }

            var relative = position - start;
            if (relative < TimeSpan.Zero) relative = TimeSpan.Zero;
            if (relative > duration) relative = duration;

            Duration = duration;
            Position = relative;
            CanSeek = duration > TimeSpan.Zero;
            UpdateProgress();
        }
        catch
        {
            ResetTimeline();
        }
    }

    private void ResetTimeline()
    {
        Position = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        Progress = 0;
        CanSeek = false;
    }

    private void UpdateProgress()
    {
        Progress = Duration <= TimeSpan.Zero
            ? 0
            : Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
    }

    private async Task RefreshPropertiesAsync()
    {
        if (_session is null) return;
        try
        {
            var props = await _session.TryGetMediaPropertiesAsync();
            Title = props.Title ?? "";
            Artist = props.Artist ?? "";

            if (props.Thumbnail is { } thumbnail)
            {
                using var winrtStream = await thumbnail.OpenReadAsync();
                using var stream = winrtStream.AsStreamForRead();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                memory.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = memory;
                image.EndInit();
                image.Freeze();
                Cover = image;
                AccentColor = ExtractAccentColor(image);
            }
            else
            {
                Cover = null;
                AccentColor = System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x34);
            }
        }
        catch
        {
            // session may have closed while reading
        }
    }

    /// <summary>Saturation-weighted average of a 32×32 downscale, brightened for use as a glow.</summary>
    private static System.Windows.Media.Color ExtractAccentColor(BitmapImage image)
    {
        try
        {
            var scaled = new System.Windows.Media.Imaging.TransformedBitmap(image,
                new System.Windows.Media.ScaleTransform(32.0 / image.PixelWidth, 32.0 / image.PixelHeight));
            var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(scaled,
                System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int width = converted.PixelWidth, height = converted.PixelHeight;
            var pixels = new byte[width * height * 4];
            converted.CopyPixels(pixels, width * 4, 0);

            double r = 0, g = 0, b = 0, totalWeight = 0;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte pb = pixels[i], pg = pixels[i + 1], pr = pixels[i + 2];
                int max = Math.Max(pr, Math.Max(pg, pb)), min = Math.Min(pr, Math.Min(pg, pb));
                double saturation = max == 0 ? 0 : (max - min) / (double)max;
                double weight = 0.05 + saturation * saturation * (max / 255.0); // favor vivid, bright pixels
                r += pr * weight;
                g += pg * weight;
                b += pb * weight;
                totalWeight += weight;
            }
            if (totalWeight <= 0) return System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x34);
            r /= totalWeight;
            g /= totalWeight;
            b /= totalWeight;

            // brighten so the glow reads on a black island
            double maxChannel = Math.Max(r, Math.Max(g, b));
            if (maxChannel > 0 && maxChannel < 190)
            {
                double boost = 190 / maxChannel;
                r = Math.Min(255, r * boost);
                g = Math.Min(255, g * boost);
                b = Math.Min(255, b * boost);
            }
            return System.Windows.Media.Color.FromRgb((byte)r, (byte)g, (byte)b);
        }
        catch
        {
            return System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x34);
        }
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }
}
