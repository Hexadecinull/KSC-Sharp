using Avalonia.Controls;
using Avalonia.Interactivity;
using KSCSharp.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace KSCSharp.App.Views;

public partial class FastFlagsWindow : Window
{
    private readonly ObservableCollection<FastFlagEntry> _items;

    public record Result(Dictionary<string, object> Flags, bool ApplyNow);

    public FastFlagsWindow() : this(new Dictionary<string, object>())
    {
        // Design-time / parameterless constructor required by the Avalonia XAML loader.
    }

    public FastFlagsWindow(Dictionary<string, object> flags)
    {
        InitializeComponent();

        _items = new ObservableCollection<FastFlagEntry>(
            flags.Select(kv => new FastFlagEntry(kv.Key, kv.Value?.ToString() ?? "")));
        FlagsList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => UpdateEmptyState();
        UpdateEmptyState();

        StatusText.Text = $"{_items.Count} flag(s) loaded.";

        BtnAddOrUpdate.Click += BtnAddOrUpdate_Click;
        BtnSave.Click += (_, _) => CloseWithResult(applyNow: false);
        BtnApplyNow.Click += (_, _) => CloseWithResult(applyNow: true);
        BtnCancel.Click += (_, _) => Close(null);
    }

    private void UpdateEmptyState() => EmptyState.IsVisible = _items.Count == 0;

    private void BtnAddOrUpdate_Click(object? sender, RoutedEventArgs e)
    {
        var key = (TxtKey.Text ?? "").Trim();
        var value = TxtValue.Text ?? "";

        if (string.IsNullOrWhiteSpace(key))
        {
            StatusText.Text = "Enter a key first.";
            return;
        }

        var existing = _items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Value = value;
        }
        else
        {
            _items.Add(new FastFlagEntry(key, value));
        }

        TxtKey.Text = "";
        TxtValue.Text = "";
        StatusText.Text = $"{_items.Count} flag(s).";
    }

    private void RemoveFlag_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FastFlagEntry entry })
        {
            _items.Remove(entry);
            StatusText.Text = $"{_items.Count} flag(s).";
        }
    }

    private void CloseWithResult(bool applyNow)
    {
        var flags = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in _items)
            flags[item.Key] = FastFlagsManager.AutoDetectValue(item.Value);

        Close(new Result(flags, applyNow));
    }
}
