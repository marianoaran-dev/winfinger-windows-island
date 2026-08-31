using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WinFinger.Models;

namespace WinFinger.Services;

/// <summary>Sticky-note persistence: pinned first, then by UpdatedAt descending.</summary>
public sealed class NoteStore
{
    public ObservableCollection<Note> Notes { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public NoteStore()
    {
        Load();
    }

    public Note Create()
    {
        var note = new Note();
        Notes.Insert(0, note);
        Save();
        return note;
    }

    public void Update(Guid id, string title, string body)
    {
        var note = Notes.FirstOrDefault(n => n.Id == id);
        if (note is null) return;
        note.Title = string.IsNullOrWhiteSpace(title) ? "Untitled note" : title;
        note.Body = body;
        note.UpdatedAt = DateTime.Now;
        Sort();
        Save();
    }

    public void TogglePin(Note note)
    {
        note.IsPinned = !note.IsPinned;
        note.UpdatedAt = DateTime.Now;
        Sort();
        Save();
    }

    public void Remove(Note note)
    {
        Notes.Remove(note);
        Save();
    }

    private void Sort()
    {
        var ordered = Notes.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt).ToList();
        for (int target = 0; target < ordered.Count; target++)
        {
            int current = Notes.IndexOf(ordered[target]);
            if (current != target)
                Notes.Move(current, target);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StoragePaths.NotesJson)) return;
            var decoded = JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(StoragePaths.NotesJson), JsonOptions);
            if (decoded is null) return;
            foreach (var note in decoded.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt))
                Notes.Add(note);
        }
        catch
        {
            // corrupt file: start fresh
        }
    }

    private void Save()
    {
        try
        {
            StoragePaths.EnsureCreated();
            File.WriteAllText(StoragePaths.NotesJson, JsonSerializer.Serialize(Notes.ToList(), JsonOptions));
        }
        catch
        {
            // best effort
        }
    }
}
