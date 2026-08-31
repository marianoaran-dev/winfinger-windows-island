# DynamicNotch fidelity checkpoint 10

Branch: `dynamicnotch-fidelity`

## Purpose

Morning handoff from autonomous overnight development into the first live Windows acceptance run with the user at the PC.

## Verified repository state

- Branch head before this checkpoint: `d982c724c21f037db56a558e2baaa134859412c3` (`docs: record battery status checkpoint`).
- Latest Windows build workflow for that head, run 36, completed successfully.
- `main` and `english-ui` have not been modified or merged as part of the DynamicNotch fidelity work.

## Overnight implementation state

The branch now contains the first substantial DynamicNotch-style Windows implementation:

- black, content-driven island shell rather than the original fixed dashboard geometry;
- damped spring/jelly resizing and dynamic corner-radius transitions;
- press interaction and content transition handling;
- persistent and temporary activity coordination with takeover and automatic restoration;
- swipe up to dismiss and swipe down to restore;
- Now Playing compact and expanded states with artwork, transport controls, timeline and seeking;
- clipboard text preview activity;
- image/screenshot preview activity with focused expanded image state;
- Pomodoro/timer compact and expanded activity integrated with the existing service;
- event-driven battery/charging temporary activity;
- deterministic state verifier and developer demo-state support;
- Windows CI restore/build/state-verification/self-contained x64 publish pipeline.

## What is not yet accepted

Do not treat the UI as visually accepted yet. The overnight work could verify code, state transitions and Windows CI, but not the actual rendered desktop experience.

Outstanding live acceptance includes:

- visual proportions and spacing against DynamicNotch;
- spring response, damping and perceived smoothness;
- press/hover/swipe feel;
- top-of-screen placement and geometry;
- DPI and multi-monitor behaviour;
- click-through/topmost/fullscreen interactions;
- actual media, clipboard, screenshot, timer and battery events on the user's Windows PC;
- frame pacing and artefacts during resizing.

## First task in the next chat

Do **not** add more features first.

Start by getting the current `dynamicnotch-fidelity` build running on the user's Windows PC and perform a short live acceptance loop. Prefer the CI/self-contained x64 artefact or build the exact branch head locally if that is the safer/current path.

Test in this order:

1. idle shell;
2. real Now Playing compact;
3. expand Now Playing and exercise transport/timeline/seek;
4. temporary clipboard text takeover and restoration;
5. screenshot/image takeover and focused expansion;
6. timer compact/expanded;
7. swipe dismiss/restore;
8. battery/charging event if convenient.

During the live run, fix high-impact visual/behavioural mismatches before adding volume, Bluetooth, Wi-Fi/VPN or other activities.

The target remains visual and behavioural fidelity to `Anubhab08/DynamicNotch` branch `codex/player-focus-clipboard-screenshot`, using the existing WinFinger Windows services where useful and keeping the surface pure black rather than Liquid Glass.
