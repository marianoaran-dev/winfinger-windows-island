# DynamicNotch fidelity checkpoint 09

Branch: `dynamicnotch-fidelity`

## Completed in this checkpoint

Added an event-driven Windows battery/power-status bridge and connected meaningful power changes to the existing DynamicNotch temporary takeover/restoration path.

- New `PowerStatusService` uses `Windows.System.Power.PowerManager` native change events rather than a polling timer.
- Tracks battery state, power-supply state and remaining charge percentage.
- Starts and stops with the existing application service lifecycle.
- Charging/power-source changes produce a short DynamicNotch notification takeover such as `Charging · 82%`.
- Discharging changes produce battery state feedback, with a low-battery icon at 20% or below.
- Remaining percentage changes do not create constant UI noise. A percentage-only notification is emitted only when crossing the 20% or 10% low-battery thresholds.
- Notifications are marshalled onto the WPF dispatcher because native power callbacks are not assumed to arrive on the UI thread.
- Timer/media remain persistent activities underneath. The existing notification coordinator handles automatic restoration after the battery takeover.

## Implementation commit

`b338c83aa7824e570fd94ac737deaccdf82f26d1` (`feat: add event-driven battery status takeovers`).

## Verification state

Windows CI run 35 was started for the implementation commit. At checkpoint creation it was still in progress, so build/publish success is intentionally **not** claimed here.

Physical Windows power-event and visual acceptance is also outstanding. CI can prove compilation and publish behaviour, but plugging/unplugging AC power and checking the actual island motion should be tested on a Windows machine.

## Recommended next task

First resolve/confirm CI run 35. If green, continue with volume as the next temporary system-state activity, preferably by reusing the existing NAudio/audio plumbing and avoiding a second competing audio-control stack. Keep battery, volume and later Wi-Fi/Bluetooth events as short takeovers over persistent timer/media activities.
