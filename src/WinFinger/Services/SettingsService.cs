using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace WinFinger.Services;

public sealed class AppSettings
{
    public bool AutoStart { get; set; }
    public bool ClipboardPaused { get; set; }
    public int PomodoroFocusMinutes { get; set; } = 25;
    public int PomodoroBreakMinutes { get; set; } = 5;
    public double IslandOffsetX { get; set; }
    public double IslandOffsetY { get; set; }
    public bool LiveGlassEnabled { get; set; } = true;
}

/// <summary>settings.json persistence + the HKCU Run auto-start key.</summary>
public sealed class SettingsService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WinFinger";

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        Load();
    }

    public void Save()
    {
        try
        {
            StoragePaths.EnsureCreated();
            File.WriteAllText(StoragePaths.SettingsJson,
                JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best effort
        }
    }

    public void SetAutoStart(bool enabled)
    {
        Settings.AutoStart = enabled;
        Save();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return;
            if (enabled)
                key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
            else
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
        catch
        {
            // registry access denied; setting stays recorded in json
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StoragePaths.SettingsJson)) return;
            Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StoragePaths.SettingsJson)) ?? new AppSettings();
        }
        catch
        {
            Settings = new AppSettings();
        }
    }
}
