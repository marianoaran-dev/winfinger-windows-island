using WinFinger.ViewModels;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void RequireGeometry(IslandGeometry actual, IslandGeometry expected, string name)
{
    Require(actual == expected, $"{name}: expected {expected}, got {actual}");
}

var state = new IslandActivityState();

Require(state.Kind == IslandActivityKind.Idle, "Initial activity must be idle.");
RequireGeometry(state.Geometry, IslandGeometry.Idle, "Idle geometry");

state.SetMediaAvailable(true);
Require(state.Kind == IslandActivityKind.Media, "Media availability must promote media activity.");
RequireGeometry(state.Geometry, IslandGeometry.MediaCompact, "Media compact geometry");

state.ToggleExpanded();
Require(state.IsExpanded, "Media activity must expand.");
RequireGeometry(state.Geometry, IslandGeometry.MediaExpanded, "Media expanded geometry");

state.ShowTemporaryNotification("♪", "Volume 42%");
Require(state.Kind == IslandActivityKind.Notification, "Temporary notification must take over.");
Require(!state.IsExpanded, "Temporary takeover must collapse the live activity.");
RequireGeometry(state.Geometry, IslandGeometry.Notification, "Notification geometry");

state.HideTemporaryNotification();
Require(state.Kind == IslandActivityKind.Media, "Media must restore after temporary notification ends.");
Require(!state.IsExpanded, "Restored media should return compact after temporary takeover.");
RequireGeometry(state.Geometry, IslandGeometry.MediaCompact, "Restored media geometry");

state.DismissCurrent();
Require(state.Kind == IslandActivityKind.Idle, "Dismissed media must leave the shell idle.");
Require(state.CanRestore, "Dismissed media must be restorable.");

state.RestoreLastDismissed();
Require(state.Kind == IslandActivityKind.Media, "Restore must recover dismissed media.");
Require(!state.CanRestore, "Restore slot must clear after restoration.");

state.SetMediaAvailable(false);
Require(state.Kind == IslandActivityKind.Idle, "Losing the media session must return to idle.");

Console.WriteLine("DynamicNotch state checks passed.");
