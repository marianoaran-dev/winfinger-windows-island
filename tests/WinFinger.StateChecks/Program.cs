using WinFinger.Models;
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
RequireGeometry(state.Geometry, IslandGeometry.MediaCompact, "Restored media geometry");

var textEntry = new ClipboardEntry(Guid.NewGuid(), ClipboardEntryKind.Text, "Hello clipboard", null,
    null, "State check", DateTime.UtcNow, "text-hash");
state.ShowTemporaryClipboard(textEntry);
Require(state.Kind == IslandActivityKind.Clipboard, "Clipboard text must take over as temporary activity.");
Require(state.ClipboardEntry == textEntry, "Clipboard activity must retain its entry payload.");
Require(!state.CanExpand, "Text clipboard preview must remain compact.");
RequireGeometry(state.Geometry, IslandGeometry.ClipboardText, "Clipboard text geometry");
state.HideTemporaryActivity();
Require(state.Kind == IslandActivityKind.Media, "Media must restore after clipboard preview ends.");

var imageEntry = new ClipboardEntry(Guid.NewGuid(), ClipboardEntryKind.Image, null, "demo.png",
    null, "Snipping Tool", DateTime.UtcNow, "image-hash");
state.ShowTemporaryClipboard(imageEntry);
Require(state.CanExpand, "Image clipboard preview must support focused expansion.");
RequireGeometry(state.Geometry, IslandGeometry.ClipboardImage, "Clipboard image geometry");
state.ToggleExpanded();
RequireGeometry(state.Geometry, IslandGeometry.ClipboardImageExpanded, "Expanded clipboard image geometry");
state.DismissCurrent();
Require(state.Kind == IslandActivityKind.Media, "Dismissing temporary clipboard content must restore persistent media.");
Require(!state.CanRestore, "Temporary clipboard dismissal must not overwrite persistent restore history.");

state.SetTimerActive(true);
Require(state.Kind == IslandActivityKind.Timer, "Active timer must take persistent priority over media.");
Require(state.CanExpand, "Timer must support expanded controls.");
RequireGeometry(state.Geometry, IslandGeometry.TimerCompact, "Timer compact geometry");
state.ToggleExpanded();
RequireGeometry(state.Geometry, IslandGeometry.TimerExpanded, "Timer expanded geometry");

state.ShowTemporaryNotification("🍅", "Focus complete. Time for a break.");
Require(state.Kind == IslandActivityKind.Notification, "Phase completion notification must temporarily take over timer.");
state.HideTemporaryNotification();
Require(state.Kind == IslandActivityKind.Timer, "Timer must restore after temporary phase notification.");
RequireGeometry(state.Geometry, IslandGeometry.TimerCompact, "Restored timer geometry");

state.DismissCurrent();
Require(state.Kind == IslandActivityKind.Idle, "Dismissed timer must leave the shell idle.");
Require(state.CanRestore, "Dismissed timer must be restorable.");
state.RestoreLastDismissed();
Require(state.Kind == IslandActivityKind.Timer, "Restore must recover dismissed timer.");
state.SetTimerActive(false);
Require(state.Kind == IslandActivityKind.Media, "Ending timer must reveal still-available media.");

state.DismissCurrent();
Require(state.Kind == IslandActivityKind.Idle, "Dismissed media must leave the shell idle.");
Require(state.CanRestore, "Dismissed media must be restorable.");
state.RestoreLastDismissed();
Require(state.Kind == IslandActivityKind.Media, "Restore must recover dismissed media.");
Require(!state.CanRestore, "Restore slot must clear after restoration.");

state.SetMediaAvailable(false);
Require(state.Kind == IslandActivityKind.Idle, "Losing the media session must return to idle.");

Console.WriteLine("DynamicNotch state checks passed.");