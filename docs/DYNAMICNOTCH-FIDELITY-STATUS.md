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

## Current architectural mismatch

`IslandWindow` currently encodes fixed presentation sizes (compact 300x36, expanded 720x480, notification 430 wide, hover 390 wide) and the expanded state opens a large generic panel. This is the primary layer to replace/refactor. Existing Windows services should not be rewritten merely for visual fidelity.

## Implementation sequence

### Phase A: shell and activity contract

- Introduce activity presentation/state model independent of individual Windows services.
- Centralise desired geometry and priority/temporary/persistent semantics.
- Keep current service APIs initially to minimise risk.
- Make black the fidelity shell baseline; do not depend on Live Glass for the target appearance.

### Phase B: Now Playing vertical slice

- Compact media view matching the reference proportions and hierarchy.
- Expanded media view matching the reference interaction pattern.
- Press compression and coordinated content/shell transition.
- Temporary test activity that suspends and restores media.
- Dismiss/restore path.

### Phase C: deterministic verification

- Add developer/demo states so visuals do not depend on real media/Bluetooth/battery events.
- Add deterministic animation progress or snapshot states where practical.
- Windows CI must build the branch before runtime acceptance is claimed.

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

- Branch isolation: confirmed, branch created from `english-ui`; `main` and `english-ui` are not modified by this workstream.
- Reference architecture/source inspection: completed for DynamicNotch `NotchEngine` and `NotchView`.
- WinFinger architecture/source inspection: initial pass completed; fixed geometry and existing service inventory confirmed.
- Windows build/runtime visual acceptance: not yet performed in this workstream.

## Next implementation task

Refactor the presentation state behind `IslandWindow` into a small activity/geometry model without changing the Windows service layer, then wire Now Playing as the first compact/expanded activity. Keep the branch buildable at each commit and add Windows CI before claiming implementation acceptance.
