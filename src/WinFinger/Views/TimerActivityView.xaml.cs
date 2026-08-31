using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using WinFinger.Services;
using WinFinger.ViewModels;

namespace WinFinger.Views;

public partial class TimerActivityView : UserControl
{
    private PomodoroService? _timer;

    public TimerActivityView()
    {
        InitializeComponent();
    }

    public void Initialize(AppViewModel model)
    {
        if (_timer is not null)
            _timer.PropertyChanged -= OnTimerChanged;

        _timer = model.Pomodoro;
        _timer.PropertyChanged += OnTimerChanged;
        Refresh();
    }

    public void SetExpanded(bool expanded, bool animate)
    {
        SetLayout(CompactLayout, !expanded, animate);
        SetLayout(ExpandedLayout, expanded, animate);
        Refresh();
    }

    private void OnTimerChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_timer is null) return;

        string phase = _timer.Phase switch
        {
            PomodoroPhase.Focus => "FOCUS",
            PomodoroPhase.Break => "BREAK",
            _ => "READY"
        };
        string status = _timer.IsRunning ? "Running" : _timer.Phase == PomodoroPhase.Idle ? "Ready" : "Paused";

        CompactPhase.Text = phase;
        CompactTime.Text = _timer.RemainingText;
        CompactStatus.Text = status;
        ExpandedPhase.Text = _timer.Phase == PomodoroPhase.Break ? "Break timer" : "Focus timer";
        ExpandedTime.Text = _timer.RemainingText;
        PrimaryButton.Content = _timer.IsRunning ? "Pause" : _timer.Phase == PomodoroPhase.Idle ? "Start" : "Resume";
        SessionCount.Text = _timer.CompletedFocusCount == 0
            ? "No focus sessions completed yet"
            : $"{_timer.CompletedFocusCount} focus session{(_timer.CompletedFocusCount == 1 ? "" : "s")} completed";

        double totalSeconds = _timer.Phase == PomodoroPhase.Break
            ? TimeSpan.FromMinutes(_timer.BreakMinutes).TotalSeconds
            : TimeSpan.FromMinutes(_timer.FocusMinutes).TotalSeconds;
        TimerProgress.Value = totalSeconds <= 0 ? 0 : 1 - Math.Clamp(_timer.Remaining.TotalSeconds / totalSeconds, 0, 1);
    }

    private void OnPrimary(object sender, RoutedEventArgs e)
    {
        if (_timer is null) return;
        if (_timer.IsRunning) _timer.Pause();
        else if (_timer.Phase == PomodoroPhase.Idle) _timer.StartFocus();
        else _timer.Resume();
        e.Handled = true;
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _timer?.Reset();
        e.Handled = true;
    }

    private static void SetLayout(UIElement element, bool visible, bool animate)
    {
        if (!animate)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            element.Opacity = visible ? 1 : 0;
            return;
        }

        if (visible)
        {
            element.Visibility = Visibility.Visible;
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)));
        }
        else if (element.Visibility == Visibility.Visible)
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(90));
            fade.Completed += (_, _) => element.Visibility = Visibility.Collapsed;
            element.BeginAnimation(OpacityProperty, fade);
        }
    }
}