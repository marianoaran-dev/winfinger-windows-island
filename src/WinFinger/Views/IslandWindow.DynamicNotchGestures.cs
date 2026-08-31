using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinFinger.Controls;

namespace WinFinger.Views;

/// <summary>
/// Gesture arbitration for the DynamicNotch fidelity surface.
/// Vertical intent dismisses/restores activities; other drags retain WinFinger's
/// useful free-positioning behaviour. Preview events prevent the two interactions
/// from fighting over the same pointer movement.
/// </summary>
public partial class IslandWindow
{
    private const double SwipeIntentThreshold = 9.0;
    private const double SwipeCommitThreshold = 44.0;
    private const double SwipeVerticalBias = 1.15;
    private const double SwipeFeedbackTravel = 24.0;

    private bool _dynamicSwipeClaimed;
    private bool _dynamicGestureSuppressed;
    private double _dynamicSwipeDeltaY;
    private TranslateTransform? _dynamicSwipeTranslate;
    private ScaleTransform? _dynamicSwipeScale;

    private void OnDynamicIslandPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        _dynamicGestureSuppressed = IsInteractiveChild(e.OriginalSource as DependencyObject);
        _dynamicSwipeClaimed = false;
        _dynamicSwipeDeltaY = 0;
        ResetSwipeFeedback(immediate: true);

        if (_dynamicGestureSuppressed) return;

        // Arm independently of the legacy mouse-down handler so gestures also work
        // while the media activity is expanded.
        _dragArmed = true;
        _dragging = false;
        _dragStartScreen = IslandBorder.PointToScreen(e.GetPosition(IslandBorder));
        _dragStartLeft = Left;
        _dragStartTop = Top;
        IslandBorder.CaptureMouse();
    }

    private void OnDynamicIslandPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dynamicGestureSuppressed || !_dragArmed || e.LeftButton != MouseButtonState.Pressed)
            return;

        var position = e.GetPosition(IslandBorder);
        var screen = IslandBorder.PointToScreen(position);
        var (deltaX, deltaY) = DeviceDeltaToDip(screen.X - _dragStartScreen.X, screen.Y - _dragStartScreen.Y);

        if (!_dynamicSwipeClaimed && !_dragging)
        {
            if (Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) < SwipeIntentThreshold)
            {
                // Keep the legacy move handler from repositioning the window while
                // the gesture direction is still undecided.
                e.Handled = true;
                return;
            }

            bool verticalIntent = Math.Abs(deltaY) > Math.Abs(deltaX) * SwipeVerticalBias;
            bool canDismiss = deltaY < 0 && _model.IslandActivity.CanDismiss;
            bool canRestore = deltaY > 0 && _model.IslandActivity.CanRestore;

            if (verticalIntent && (canDismiss || canRestore))
                _dynamicSwipeClaimed = true;
            else
                _dragging = true;
        }

        if (_dynamicSwipeClaimed)
        {
            _dynamicSwipeDeltaY = deltaY;
            ApplySwipeFeedback(deltaY);
            e.Handled = true;
            return;
        }

        if (_dragging)
        {
            Left = _dragStartLeft + deltaX;
            Top = _dragStartTop + deltaY;
            ClampPosition();
            CaptureGlass();
            e.Handled = true;
        }
    }

    private void OnDynamicIslandPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _dynamicGestureSuppressed)
            return;

        if (!_dynamicSwipeClaimed)
            return; // Existing mouse-up path owns click and reposition completion.

        bool commit = Math.Abs(_dynamicSwipeDeltaY) >= SwipeCommitThreshold;
        bool dismiss = commit && _dynamicSwipeDeltaY < 0 && _model.IslandActivity.CanDismiss;
        bool restore = commit && _dynamicSwipeDeltaY > 0 && _model.IslandActivity.CanRestore;

        _dynamicSwipeClaimed = false;
        _dragArmed = false;
        _dragging = false;
        IslandBorder.ReleaseMouseCapture();
        ResetSwipeFeedback(immediate: false);

        if (dismiss)
            _model.IslandActivity.DismissCurrent();
        else if (restore)
            _model.IslandActivity.RestoreLastDismissed();

        // A committed or cancelled vertical swipe is never also a click.
        e.Handled = true;
    }

    private (double X, double Y) DeviceDeltaToDip(double x, double y)
    {
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var dip = transform.Transform(new Point(x, y));
        return (dip.X, dip.Y);
    }

    private void EnsureSwipeTransforms()
    {
        if (_dynamicSwipeTranslate is not null && _dynamicSwipeScale is not null) return;

        _dynamicSwipeScale = new ScaleTransform(1, 1);
        _dynamicSwipeTranslate = new TranslateTransform(0, 0);

        var group = new TransformGroup();
        if (_pressScale is not null)
            group.Children.Add(_pressScale);
        group.Children.Add(_dynamicSwipeScale);
        group.Children.Add(_dynamicSwipeTranslate);

        IslandBorder.RenderTransformOrigin = new Point(0.5, 0);
        IslandBorder.RenderTransform = group;
    }

    private void ApplySwipeFeedback(double deltaY)
    {
        EnsureSwipeTransforms();
        if (_dynamicSwipeTranslate is null || _dynamicSwipeScale is null) return;

        double progress = Math.Clamp(Math.Abs(deltaY) / (SwipeCommitThreshold * 1.45), 0, 1);
        double direction = Math.Sign(deltaY);
        double travel = direction * SwipeFeedbackTravel * Math.Pow(progress, 0.82);

        _dynamicSwipeTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        _dynamicSwipeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _dynamicSwipeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        IslandBorder.BeginAnimation(OpacityProperty, null);

        _dynamicSwipeTranslate.Y = travel;
        _dynamicSwipeScale.ScaleX = 1 + 0.025 * progress;
        _dynamicSwipeScale.ScaleY = 1 - 0.035 * progress;
        IslandBorder.Opacity = 1 - 0.24 * progress;
    }

    private void ResetSwipeFeedback(bool immediate)
    {
        if (_dynamicSwipeTranslate is null || _dynamicSwipeScale is null)
        {
            IslandBorder.Opacity = 1;
            return;
        }

        if (immediate)
        {
            _dynamicSwipeTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            _dynamicSwipeScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _dynamicSwipeScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            IslandBorder.BeginAnimation(OpacityProperty, null);
            _dynamicSwipeTranslate.Y = 0;
            _dynamicSwipeScale.ScaleX = 1;
            _dynamicSwipeScale.ScaleY = 1;
            IslandBorder.Opacity = 1;
            return;
        }

        const double duration = 0.46;
        var spring = new DampedSpringEase
        {
            EasingMode = EasingMode.EaseIn,
            ResponseSeconds = 0.42,
            DampingFraction = 0.74,
            DurationSeconds = duration
        };

        _dynamicSwipeTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromSeconds(duration)) { EasingFunction = spring });
        _dynamicSwipeScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, TimeSpan.FromSeconds(duration)) { EasingFunction = spring });
        _dynamicSwipeScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, TimeSpan.FromSeconds(duration)) { EasingFunction = spring });
        IslandBorder.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private static bool IsInteractiveChild(DependencyObject? source)
    {
        for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase)
                return true;
        }
        return false;
    }
}
