using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using SoundMist.Models;
using SoundMist.ViewModels;
using System;
using System.Diagnostics;

namespace SoundMist.Views;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _vm;

    public SettingsView()
    {
        InitializeComponent();
        DataContext = _vm = App.GetService<SettingsViewModel>();
    }

    private void RemoveBlockedTrack(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var entry = GetBlockedEntry(sender) ?? throw new NullReferenceException($"The {nameof(RemoveBlockedTrack)} has to be called by a button within a ItemsControl of blocked entries");

        _vm.RemoveBlockedTrack(entry);
    }

    private void RemoveBlockedUser(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var entry = GetBlockedEntry(sender) ?? throw new NullReferenceException($"The {nameof(RemoveBlockedUser)} has to be called by a button within a ItemsControl of blocked entries");

        _vm.RemoveBlockedUser(entry);
    }

    private static BlockedEntry? GetBlockedEntry(object? sender)
    {
        if (sender is not Button b)
            return null;

        var item = b.FindAncestorOfType<ContentPresenter>();
        var entry = item?.Content as BlockedEntry;
        return entry;
    }

    private void OnlyNumericKeysDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (!int.TryParse(e.KeySymbol, out int _))
            e.Handled = true;
    }

    private void NumericUpDown_ValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
    {
        if (!e.NewValue.HasValue)
        {
            var c = (NumericUpDown)sender;
            c.Value = e.OldValue;
        }
    }
}