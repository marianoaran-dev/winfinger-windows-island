using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using WinFinger.Services;
using WinFinger.ViewModels;

namespace WinFinger.Views.Pages;

public partial class PomodoroPage : UserControl, IIslandPage
{
    private AppViewModel? _model;

    public PomodoroPage()
    {
        InitializeComponent();
    }

    public void Initialize(AppViewModel model)
    {
        _model = model;
        var pomodoro = model.Pomodoro;

        StartButton.Click += (_, _) =>
        {
            if (pomodoro.IsRunning) pomodoro.Pause();
            else if (pomodoro.Phase == PomodoroPhase.Idle) pomodoro.StartFocus();
            else pomodoro.Resume();
        };
        ResetButton.Click += (_, _) => pomodoro.Reset();

        FocusMinus.Click += (_, _) => pomodoro.FocusMinutes = Math.Max(5, pomodoro.FocusMinutes - 5);
        FocusPlus.Click += (_, _) => pomodoro.FocusMinutes = Math.Min(90, pomodoro.FocusMinutes + 5);
        BreakMinus.Click += (_, _) => pomodoro.BreakMinutes = Math.Max(1, pomodoro.BreakMinutes - 1);
        BreakPlus.Click += (_, _) => pomodoro.BreakMinutes = Math.Min(30, pomodoro.BreakMinutes + 1);

        pomodoro.PropertyChanged += OnPomodoroChanged;
        Refresh();
    }

    public void OnShown() => Refresh();

    private void OnPomodoroChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_model is null) return;
        var pomodoro = _model.Pomodoro;

        TimeLabel.Text = pomodoro.RemainingText;
        PhaseLabel.Text = pomodoro.Phase switch
        {
            PomodoroPhase.Focus => pomodoro.IsRunning ? "Focusing" : "Focus paused",
            PomodoroPhase.Break => pomodoro.IsRunning ? "On break" : "Break paused",
            _ => "Ready to focus"
        };
        TimeLabel.Foreground = pomodoro.Phase == PomodoroPhase.Break
            ? (Brush)FindResource("Brush.Green")
            : (Brush)FindResource("Brush.TextPrimary");

        StartLabel.Text = pomodoro.IsRunning ? "Pause" : pomodoro.Phase == PomodoroPhase.Idle ? "Start focus" : "Resume";
        StartLabel.Foreground = pomodoro.IsRunning
            ? (Brush)FindResource("Brush.Orange")
            : (Brush)FindResource("Brush.Green");

        FocusLabel.Text = $"{pomodoro.FocusMinutes} min";
        BreakLabel.Text = $"{pomodoro.BreakMinutes} min";
        StatsLabel.Text = pomodoro.CompletedFocusCount > 0
            ? $"{pomodoro.CompletedFocusCount} focus session{(pomodoro.CompletedFocusCount == 1 ? "" : "s")} completed today"
            : "";
    }
}
