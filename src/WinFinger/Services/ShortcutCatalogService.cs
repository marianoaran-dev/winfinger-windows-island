using System.IO;
using System.Reflection;
using System.Text.Json;
using WinFinger.Models;

namespace WinFinger.Services;

/// <summary>Static per-app shortcut dictionary, embedded as Resources/shortcuts.json.</summary>
public sealed class ShortcutCatalogService
{
    private readonly List<ShortcutSet> _sets;

    public ShortcutCatalogService()
    {
        _sets = LoadEmbedded() ?? new List<ShortcutSet>();
    }

    public ShortcutSet SetFor(string? processName)
    {
        if (!string.IsNullOrEmpty(processName))
        {
            var lower = processName.ToLowerInvariant();
            var match = _sets.FirstOrDefault(s => s.ProcessNames.Contains(lower));
            if (match is not null) return match;
        }
        return _sets.FirstOrDefault(s => s.Id == "generic") ?? GenericFallback;
    }

    private static List<ShortcutSet>? LoadEmbedded()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("shortcuts.json", StringComparison.OrdinalIgnoreCase));
            if (name is null) return null;
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<List<ShortcutSet>>(reader.ReadToEnd());
        }
        catch
        {
            return null;
        }
    }

    private static readonly ShortcutSet GenericFallback = new(
        "generic", "Windows", Array.Empty<string>(),
        new[]
        {
            new ShortcutGroup("editing", "Editing", new[]
            {
                new ShortcutItem("copy", "Ctrl+C", "Copy"),
                new ShortcutItem("paste", "Ctrl+V", "Paste"),
                new ShortcutItem("cut", "Ctrl+X", "Cut"),
                new ShortcutItem("undo", "Ctrl+Z", "Undo"),
                new ShortcutItem("select-all", "Ctrl+A", "Select all")
            })
        });
}
