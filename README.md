# WinFinger

A Windows Dynamic Island productivity utility, ported from the macOS notch utility **MacFinger** and enhanced with Dynamic Island-style interactions.

WinFinger keeps an iOS Dynamic Island-inspired Liquid Glass capsule at the top of the screen. In compact mode it shows live network speed and memory usage. Click it to expand into a five-page panel: **Clipboard History / Media Controls / Sticky Notes / Shortcuts / Pomodoro**.

## Download

Go to [Releases](../../releases) and download the latest `WinFinger.exe` package. The published build is self-contained, so .NET does not need to be installed separately.

## Features

| Module | Description |
|---|---|
| Compact island | Top-centred black/glass capsule. Shows album artwork while media is playing, plus live `↓ download ↑ upload · memory %` metrics refreshed every second. |
| Clipboard history | Event-driven monitoring using `WM_CLIPBOARDUPDATE`. Supports text and images, SHA-256 deduplication, up to 100 entries, PNG image storage, pause/clear/restore actions and source-application tracking. |
| Media controls | Uses the Windows global media session API (GSMTC) for artwork, title, artist, play/pause and previous/next controls. Supports Spotify, browsers and other compatible media apps. |
| Sticky notes | Notes list and editor with automatic save after a 500 ms debounce, pinned-note sorting and `Ctrl+N` for a new note. |
| Shortcut catalogue | Automatically changes according to the foreground application, including File Explorer, Chrome, Edge, VS Code, Word, Excel, WeChat and Windows Terminal. Falls back to general Windows shortcuts when no specific match exists. |
| Pomodoro | Configurable focus/break cycle. The compact island shows the countdown and displays an in-island alert with sound when a phase completes. |
| In-island notifications | Events such as clipboard captures and Pomodoro completion temporarily expand the capsule into a notification strip for three seconds. |
| Audio visualiser | Eight spectrum bars animate in real time while media is playing, using WASAPI loopback and FFT analysis. |
| Artwork-colour glow | Extracts the dominant album-art colour and uses it for a softly pulsing glow around the island during playback. |
| Hover pre-expand | Hovering over the capsule slightly enlarges it and reveals the current track title. Click to fully expand. |
| Liquid Glass | Custom real-time glass effect: captures the screen behind the island, downsamples and blurs it, increases saturation by 1.6×, then renders it behind the island. Includes a refractive rim, animated edge lighting, chromatic fringe, top highlight, expansion shimmer and artwork-colour bleed. Windows Acrylic was not used because it desaturates the background too strongly. |
| Ghost mode | When the pointer is away, the island fades to 40% opacity and becomes click-through so it does not block browser tabs. It becomes interactive again when the pointer approaches. |
| Drag to reposition | Drag the capsule horizontally to reposition it. The location is remembered between launches. |

## Interaction

- **Click the capsule** to expand it. Press **Esc** or click outside the panel to collapse it.
- **Ctrl+1..5** switches pages: Clipboard / Media / Notes / Shortcuts / Pomodoro.
- Tray icon actions include Open, Pause Clipboard History, Clear Clipboard History, Start with Windows and Exit.
- Tray → **Appearance…** provides three background modes: Live Glass, solid colour or custom image. It includes an HSV colour picker and hex input, image dimming, glass darkness/saturation, lighting-effect toggles and live preview. Settings are remembered.
- In solid-colour or image mode, live screen capture stops completely, eliminating that extra processing overhead.
- The island does not appear on the taskbar or in Alt+Tab.

## Technology

WPF / .NET 8 (`net8.0-windows10.0.19041.0`) with embedded CsWinRT projections for the WinRT media APIs. MVVM uses CommunityToolkit.Mvvm, and the tray icon uses Hardcodet.NotifyIcon.Wpf. Per-Monitor V2 DPI is supported.

### Window implementation

The application uses a transparent, borderless, always-on-top window at maximum size. Only the internal island Border's width, height and corner radius are animated. Expansion uses a 280 ms `BackEase` spring animation; collapse uses a 180 ms `CubicEase`. Transparent pixels naturally allow pointer input to pass through.

### Glass implementation

Windows' built-in Acrylic (`DWM SystemBackdrop`) desaturates the background too heavily for the intended Liquid Glass effect, so WinFinger implements its own renderer.

At 12.5 fps, GDI `StretchBlt` captures the screen region behind the island and downsamples it directly into a 128×84 DIB. Downsampling provides the initial blur, followed by two box-blur passes, 1.6× saturation enhancement and brightness adjustment. The result is written to a `WriteableBitmap` and rendered as the island's background `ImageBrush`.

The island window uses `WDA_EXCLUDEFROMCAPTURE` to prevent feedback loops when capturing the screen behind itself.

> **Note:** Because of `WDA_EXCLUDEFROMCAPTURE`, the island does **not** appear in screenshots, screen recordings or screen sharing. To photograph the island itself, use an external camera.

> **Known limitation:** WinFinger does not intercept Windows system Toast notifications. `UserNotificationListener` requires MSIX package identity, which an unpackaged executable does not have. In-island notifications therefore cover only WinFinger's own events.

## Build

Requires the .NET 8 SDK:

```bash
dotnet build
dotnet run --project src/WinFinger
```

Publish a self-contained single-file x64 build (approximately 75 MB including the runtime):

```bash
dotnet publish src/WinFinger/WinFinger.csproj -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

Output:

```text
src/WinFinger/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/WinFinger.exe
```

## Local data

```text
%APPDATA%\WinFinger\
├── clipboard.json      # Clipboard metadata
├── notes.json          # Sticky notes
├── settings.json       # Settings
└── ClipboardMedia\     # Clipboard image PNG files
```

## Requirements

- Windows 10 1809 or later; Windows 11 is recommended.
- Clipboard history may contain sensitive information. Clipboard history is stored as plaintext on the local disk. You can pause recording or clear the history at any time.

## Licence

For learning and personal use only.
