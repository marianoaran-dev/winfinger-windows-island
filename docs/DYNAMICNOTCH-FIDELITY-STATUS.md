# DynamicNotch Fidelity Workstream

Target branch: `dynamicnotch-fidelity`
Reference: `Anubhab08/DynamicNotch` branch `codex/player-focus-clipboard-screenshot`

## Goal

Preserve WinFinger's useful Windows integration while replacing its fixed dashboard presentation with a content-driven black island that closely follows DynamicNotch's visual and interaction model.

## Reference behaviours verified

DynamicNotch's shell is content-driven rather than a fixed dashboard. The active content supplies compact and expanded views; the shell animates to the presented size. Its engine maintains persistent live activities and temporary notifications, priority ordering, suspension/restoration, dismissal/restoration and queued transitions. The view coordinates size, corner geometry, press scale, shadow, clipping, content opacity/blur and content transitions.

DynamicNotch's balanced animation preset has now also been inspected directly. Its live-activity expansion uses a spring response of about 0.45 s with damping fraction 0.75; normal content/show transitions are around a 0.47 s response. WinFinger now has a damped-harmonic WPF easing function expressed in the same response/damping vocabulary instead of relying only on `BackEase`.

## First vertical slice acceptance target

1. Idle black capsule.
2. Now Playing compact activity.
3. Click/press expands into the full Now Playing activity rather than a generic dashboard.
4. Temporary activity can take over the island.
5. When temporary activity ends, Now Playing is restored automatically.
6. Active content can be dismissed and restored.
7. Width, height and corner radius morph continuously with spring-like motion.
8. Content transition is coordinated with shell resizing, with no obvious clipping/jump.

## Existing WinFinger assets preserved

- WPF transparent/topmost window and desktop positioning.
- Windows media session integration and transport controls.
- Album artwork.
- Clipboard monitoring.
- Pomodoro/timer service.
- Notification plumbing.
- Tray/settings/autostart.
- Legacy expanded pages remain available as a compatibility fallback until migrated into activities.

## Current implementation state

### Activity presentation model

`IslandActivityState` owns the presentation-level activity state independently of Windows services. It defines idle, media and temporary-notification activities, content-driven geometry, media compact/expanded states, temporary takeover/restoration, dismissal and restoration. Current target geometries are intentionally small and reference-like rather than dashboard-sized:

- Idle: 184 x 34, radius 17
- Media compact: 320 x 64, radius 24
- Media expanded: 430 x 210, radius 30
- Notification: 360 x 52, radius 22

`AppViewModel` feeds media-session availability into this coordinator while leaving the existing `MediaService` intact.

### Dynamic activity shell

`IslandWindow.DynamicNotch.cs` now takes over the fidelity presentation without rewriting the large legacy window implementation. It:

- Initialises and renders the dedicated media activity.
- Makes `IslandActivityState.Geometry` drive width, height and corner radius.
- Uses an all-black baseline instead of Live Glass, chromatic edges, glints and media tinting.
- Keeps the old 720 x 480 expanded panel only for unmigrated legacy page commands.
- Clicks compact media into the dedicated expanded media activity instead of the generic dashboard.
- Adds top-anchored press compression and spring release.
- Routes existing WinFinger notifications through `IslandActivityState` so they temporarily take over and automatically restore persistent media.
- Coalesces multi-property state changes so a temporary takeover does not animate through unintended intermediate states.
- Adjusts shadow strength based on expanded geometry.

### Dedicated media presentation

`MediaActivityView` has separate compact and expanded Now Playing layouts using the existing Windows media service for title, artist, artwork, play/pause, previous and next. The expanded layout remains a small player surface rather than a five-tab dashboard.

### Motion fidelity

A new `DampedSpringEase` implements an under-damped second-order response for WPF animation. The shell is now tuned from DynamicNotch's balanced spring values rather than using generic WPF back easing for its main geometry morph. Runtime visual tuning is still required before claiming matching feel/frame pacing.

### Deterministic verification

A package-free `tests/WinFinger.StateChecks` executable verifies the first presentation sequence:

idle -> media compact -> media expanded -> temporary notification -> media compact restore -> dismiss -> restore -> idle when media disappears.

Windows CI now executes this state verifier between build and publish. The CI commit that introduced the state check completed successfully, including build, verifier, publish and artefact upload.

The presentation layer also recognises the developer-only `WINFINGER_DYNAMICNOTCH_DEMO` environment variable with deterministic states: `idle`, `media-compact`, `media-expanded`, and `notification`. This is intended for repeatable screenshots/runtime comparison on a real Windows desktop without requiring live media or system events.

## Implementation sequence

### Phase A: shell and activity contract

- [x] Introduce activity presentation/state model independent of individual Windows services.
- [x] Centralise first desired geometry and temporary/persistent semantics.
- [x] Keep current service APIs initially to minimise risk.
- [x] Make the shell state renderer consume `IslandActivityState` directly.
- [x] Make black the fidelity shell baseline rather than depending on Live Glass.

### Phase B: Now Playing vertical slice

- [x] Add dedicated compact/expanded media activity view.
- [x] Bind it to the existing Windows media session service.
- [x] Route media activity into the shell and morph to its geometry.
- [x] Add press compression and coordinated content/shell transition.
- [x] Temporary notification suspends/restores media through the coordinator.
- [ ] Wire a DynamicNotch-style dismiss/restore gesture into interaction (state semantics already exist and are CI-verified).
- [ ] Add richer expanded-player fidelity such as progress/scrubbing and reference-accurate spacing after runtime visual comparison.

### Phase C: deterministic verification

- [x] Add Windows CI build + self-contained x64 publish workflow.
- [x] Add deterministic coordinator state checks to CI.
- [x] Add developer/demo states so visuals do not depend on real media/Bluetooth/battery events.
- [ ] Add deterministic screenshot capture or equivalent visual regression path on Windows.

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
- Reference architecture/source inspection: completed for DynamicNotch engine, view and balanced animation presets.
- Dynamic shell integration commit: Windows CI restore/build/publish completed successfully.
- Coordinator state verifier + CI integration: completed successfully.
- Latest damped-spring tuning commit: CI is queued/running at this handoff, so it is not yet marked verified.
- Windows desktop visual/runtime acceptance: not yet performed.

## Next implementation task

First confirm the damped-spring head commit passes Windows CI. Then add a reference-like dismiss/restore gesture without sacrificing a deliberate way to reposition the Windows island. After that, use the deterministic demo states for the first real Windows visual comparison and tune geometry/spacing/motion. If physical desktop access is still unavailable, continue with the next independent activities, preferably screenshot and clipboard because WinFinger already has useful plumbing for both.
