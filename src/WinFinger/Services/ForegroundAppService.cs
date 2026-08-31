using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using WinFinger.Interop;

namespace WinFinger.Services;

/// <summary>Tracks the foreground application via a WinEvent hook.</summary>
public sealed partial class ForegroundAppService : ObservableObject
{
    /// <summary>Lowercase process name of the last foreground app (excluding WinFinger itself).</summary>
    [ObservableProperty] private string? _processName;
    [ObservableProperty] private string _displayName = "Current app";

    private IntPtr _hook;
    private NativeMethods.WinEventDelegate? _hookDelegate; // field: keeps delegate alive against GC
    private readonly string _selfName = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();

    public void Start()
    {
        Update(NativeMethods.GetForegroundWindow());
        _hookDelegate = OnWinEvent;
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _hookDelegate, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
        _hookDelegate = null;
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint thread, uint time)
    {
        // WINEVENT_OUTOFCONTEXT delivers on our UI thread's message loop, but marshal defensively.
        Application.Current?.Dispatcher.BeginInvoke(() => Update(hwnd));
    }

    private void Update(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero) return;
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return;
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName.ToLowerInvariant();
            if (name == _selfName) return; // keep showing the previous app while our panel has focus

            ProcessName = name;
            DisplayName = FriendlyName(process);
        }
        catch
        {
            // process exited between events; keep last value
        }
    }

    private static string FriendlyName(Process process)
    {
        try
        {
            var description = process.MainModule?.FileVersionInfo.FileDescription;
            if (!string.IsNullOrWhiteSpace(description)) return description;
        }
        catch
        {
            // access denied for elevated processes
        }
        return process.ProcessName;
    }
}
