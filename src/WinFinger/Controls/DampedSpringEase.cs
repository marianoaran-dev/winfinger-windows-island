using System.Windows;
using System.Windows.Media.Animation;

namespace WinFinger.Controls;

/// <summary>
/// Small damped-harmonic easing curve used to approximate SwiftUI's spring motion.
/// ResponseSeconds and DampingFraction intentionally mirror the vocabulary used by
/// DynamicNotch's animation presets so motion can be tuned from the reference values.
/// </summary>
public sealed class DampedSpringEase : EasingFunctionBase
{
    public static readonly DependencyProperty ResponseSecondsProperty = DependencyProperty.Register(
        nameof(ResponseSeconds), typeof(double), typeof(DampedSpringEase),
        new PropertyMetadata(0.45));

    public static readonly DependencyProperty DampingFractionProperty = DependencyProperty.Register(
        nameof(DampingFraction), typeof(double), typeof(DampedSpringEase),
        new PropertyMetadata(0.75));

    public static readonly DependencyProperty DurationSecondsProperty = DependencyProperty.Register(
        nameof(DurationSeconds), typeof(double), typeof(DampedSpringEase),
        new PropertyMetadata(0.68));

    public double ResponseSeconds
    {
        get => (double)GetValue(ResponseSecondsProperty);
        set => SetValue(ResponseSecondsProperty, value);
    }

    public double DampingFraction
    {
        get => (double)GetValue(DampingFractionProperty);
        set => SetValue(DampingFractionProperty, value);
    }

    public double DurationSeconds
    {
        get => (double)GetValue(DurationSecondsProperty);
        set => SetValue(DurationSecondsProperty, value);
    }

    protected override double EaseInCore(double normalizedTime)
    {
        double response = Math.Max(0.05, ResponseSeconds);
        double damping = Math.Clamp(DampingFraction, 0.01, 0.999);
        double duration = Math.Max(0.05, DurationSeconds);
        double time = Math.Clamp(normalizedTime, 0, 1) * duration;

        // Standard under-damped second-order step response. Using 2π / response
        // makes the response value behave like SwiftUI's perceptual spring period.
        double omega0 = 2 * Math.PI / response;
        double omegaD = omega0 * Math.Sqrt(1 - damping * damping);
        double envelope = Math.Exp(-damping * omega0 * time);
        double phase = damping / Math.Sqrt(1 - damping * damping);
        double value = 1 - envelope * (Math.Cos(omegaD * time) + phase * Math.Sin(omegaD * time));

        return value;
    }

    protected override Freezable CreateInstanceCore() => new DampedSpringEase();
}
