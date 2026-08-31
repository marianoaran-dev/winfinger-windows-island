using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WinFinger.Models;

/// <summary>A sticky note (field-compatible with mac's notes.json).</summary>
public sealed class Note : INotifyPropertyChanged
{
    private string _title = "Untitled note";
    private string _body = "";
    private bool _isPinned;
    private DateTime _updatedAt = DateTime.Now;

    [JsonPropertyName("id")] public Guid Id { get; init; } = Guid.NewGuid();
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; } = DateTime.Now;

    [JsonPropertyName("title")]
    public string Title
    {
        get => _title;
        set => Set(ref _title, value, nameof(Title));
    }

    [JsonPropertyName("body")]
    public string Body
    {
        get => _body;
        set => Set(ref _body, value, nameof(Body));
    }

    [JsonPropertyName("isPinned")]
    public bool IsPinned
    {
        get => _isPinned;
        set => Set(ref _isPinned, value, nameof(IsPinned));
    }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => Set(ref _updatedAt, value, nameof(UpdatedAt));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, string name)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
