using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WinFinger.Models;
using WinFinger.ViewModels;

namespace WinFinger.Views.Pages;

public partial class ClipboardPage : UserControl, IIslandPage
{
    private AppViewModel? _model;

    public ClipboardPage()
    {
        InitializeComponent();
    }

    public void Initialize(AppViewModel model)
    {
        _model = model;
        EntryList.ItemsSource = model.ClipboardStore.Entries;

        PauseButton.Click += (_, _) =>
        {
            model.ClipboardMonitor.IsPaused = !model.ClipboardMonitor.IsPaused;
            PauseButton.Content = model.ClipboardMonitor.IsPaused ? "继续记录" : "暂停记录";
        };
        ClearButton.Click += (_, _) => model.ClipboardStore.Clear();

        model.ClipboardStore.Entries.CollectionChanged += OnEntriesChanged;
        UpdateEmptyHint();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateEmptyHint();

    private void UpdateEmptyHint() =>
        EmptyHint.Visibility = _model?.ClipboardStore.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnCopyEntry(object sender, RoutedEventArgs e)
    {
        if (_model is not null && ((FrameworkElement)sender).Tag is ClipboardEntry entry)
            _model.ClipboardMonitor.CopyToClipboard(entry);
    }

    private void OnPreviewImage(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not ClipboardEntry entry ||
            string.IsNullOrEmpty(entry.ImagePath) || !System.IO.File.Exists(entry.ImagePath)) return;
        e.Handled = true;
        try
        {
            var win = new ImagePreviewWindow(entry.ImagePath);
            win.Show();
            win.Activate(); // island is NOACTIVATE; the lightbox needs focus so Esc closes it
        }
        catch
        {
            // image file unreadable: ignore, thumbnail already hinted at the problem
        }
    }

    private void OnDeleteEntry(object sender, RoutedEventArgs e)
    {
        if (_model is not null && ((FrameworkElement)sender).Tag is ClipboardEntry entry)
            _model.ClipboardStore.Remove(entry);
    }
}
