# DynamicNotch Fidelity Workstream

Target branch: `dynamicnotch-fidelity`
Reference: `Anubhab08/DynamicNotch` branch `codex/player-focus-clipboard-screenshot`

## Goal

Preserve WinFinger's useful Windows integration while replacing its fixed dashboard presentation with a content-driven black island that closely follows DynamicNotch's visual and interaction model.

## Reference behaviours verified

DynamicNotch's shell is content-driven rather than a fixed dashboard. The active content supplies compact and expanded views; the shell animates to the presented size. Its engine maintains persistent live activities and temporary notifications, priority ordering, suspension/restoration, dismissal/restoration and queued transitions. The view coordinates size, corner geometry, press scale, shadow, clipping, content opacity/blur and content transitions.

## First vertical slice acceptance target

1. Idle black capsule.
2. Now Playing compact activity.
3. Click/press expands into the full Now Playing activity rather than a generic dashboard.
4. Temporary activity can take over the island.
5. When temporary activity ends, Now Playing is restored automatically.
6. Active content can be dismissed and restored.
7. Width, height and corner radius morph continuously with spring-like motion.
8. Content transition is coordinated with shell resizing, with no obvious clipping/jump.

## Existing WinFinger assets to preserve

- WPF transparent/topmost window and desktop positioning.
- Windows media session integration and transport controls.
- Album artwork.
- Clipboard monitoring.
- Pomodoro/timer service.
- Notification plumbing.
- Tray/settings/autostart.

## Current implementation state

### Activity presentation model

`IslandActivityState` now owns the first presentation-level activity state independently of Windows services. It defines idle, media and temporary-notification activities, content-driven geometry, media compact/expanded states, temporary takeover/restoration, dismissal and restoration. Current target geometries are intentionally small and reference-like rather than dashboard-sized:

- Idle: 184 x 34, radius 17
- Media compact: 320 x 64, radius 24
- Media expanded: 430 x 210, radius 30
- Notification: 360 x 52, radius 22

`AppViewModel` feeds media-session availability into this coordinator while leaving the existing `MediaService` intact.

### Dedicated media presentation

A new `MediaActivityView` exists with separate compact and expanded Now Playing layouts. It uses the existing Windows media service for title, artist, cover art, play/pause, previous and next. The expanded layout is a compact player surface, not the old 720 x 480 five-tab dashboard.

The island shell now hosts this media activity view and its baseline XAML geometry has been reduced from the old 300 x 36 pill to the new 184 x 34 idle geometry.

Important: the new media activity is hosted but not yet switched by `IslandWindow.xaml.cs`; the next step is the integration commit that makes `IslandActivityState` drive shell geometry/content and replaces the legacy expand path for media. Do not claim the vertical slice is runtime-complete yet.

## Implementation sequence

### Phase A: shell and activity contract

- [x] Introduce activity presentation/state model independent of individual Windows services.
- [x] Centralise first desired geometry and temporary/persistent semantics.
- [x] Keep current service APIs initially to minimise risk.
- [ ] Make the shell state renderer consume `IslandActivityState` directly.
- [ ] Make black the fidelity shell baseline rather than depending on Live Glass.

### Phase B: Now Playing vertical slice

- [x] Add dedicated compact/expanded media activity view.
- [x] Bind it to the existing Windows media session service.
- [ ] Route media activity into the shell and morph to its geometry.
- [ ] Press compression and coordinated content/shell transition.
- [ ] Temporary notification suspends/restores media through the coordinator.
- [ ] Dismiss/restore path wired to interaction.

### Phase C: deterministic verification

- [x] Add Windows CI build + self-contained x64 publish workflow.
- [ ] Add developer/demo states so visuals do not depend on real media/Bluetooth/battery events.
- [ ] Add deterministic animation progress or snapshot states where practical.

### Phase D: activity migration/addition

Screenshot, clipboard, timer, charging/battery, Bluetooth, volume, Wi-Fi/VPN, screen recording/downloads where Windows APIs permit equivalent behaviour.

## Fidelity principles

- Reproduce observed behaviour and geometry, not Swift implementation details.
- Do not copy GPL source into WinFinger.
- Prefer one coherent island renderer/activity engine over per-feature animation hacks.
- Keep settings in a normal settings window.
- Do not make legacy RAM/network widgets central to the island.
- Treat physical Windows desktop behaviour (DPI, multi-monitor, click-through, frame pacing, fullscreen interaction) as runtime acceptance, not something inferred from source review.

## Verification state

- Branch isolation: confirmed; `main` and `english-ui` remain untouched.
- Reference architecture/source inspection: completed for DynamicNotch `NotchEngine` and `NotchView`.
- Windows CI: workflow is now proven operational. The activity model and AppViewModel integration commits both completed restore/build/publish successfully on `windows-latest`.
- Media activity/XAML-host commits: CI runs were still executing when this status was updated, so they are not yet marked verified here.
- Windows desktop visual/runtime acceptance: not yet performed.

## Next implementation task

Integrate `IslandActivityState` into `IslandWindow.xaml.cs`: initialise `MediaActivityView`, switch idle/media/notification content from coordinator state, morph to `Geometry`, and make media clicks expand/collapse the dedicated player. Keep the old generic panel path available only for legacy tray/page commands until those features are migrated. Then add press-scale animation and deterministic demo states before moving on to screenshot/clipboard activities.
