using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WINbackup.Services;

namespace WINbackup.Models;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Item de lista com seleção, usado por quase todas as tabelas e cartões das telas.</summary>
public class CheckItem : Observable
{
    public string Key { get; init; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Col1 { get; set; } = "";
    public string Col2 { get; set; } = "";
    public string Col3 { get; set; } = "";
    public string Meta { get; set; } = "";
    public Brush MetaBrush { get; set; } = UI.Muted;
    public Brush Fill { get; set; } = UI.Transparent;
    public Thickness Indent { get; set; } = new(18, 11, 18, 11);
    public Windows.UI.Text.FontWeight Weight { get; set; } = Microsoft.UI.Text.FontWeights.Normal;
    public string Icon { get; set; } = "";
    public bool Flag { get; set; }
    public bool IsAddButton { get; init; }
    public object? Data { get; set; }

    public Visibility FlagVisibility => Flag ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ItemVisibility => IsAddButton ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AddVisibility => IsAddButton ? Visibility.Visible : Visibility.Collapsed;

    bool _enabled = true;
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

    bool _checked;
    public bool IsChecked
    {
        get => _checked;
        set { if (Set(ref _checked, value)) Changed?.Invoke(this); }
    }

    public event Action<CheckItem>? Changed;

    public void Refresh() => Raise(string.Empty);
}

public enum StepState { Waiting, Running, Done, Warning, Error, Skipped, Unavailable }

public class StepItem : Observable
{
    public string Name { get; init; } = "";
    public string Module { get; init; } = "";

    string _status = L.T("Na fila");
    public string Status { get => _status; set => Set(ref _status, value); }

    StepState _state;
    public StepState State
    {
        get => _state;
        set { if (Set(ref _state, value)) { Raise(nameof(Ring)); Raise(nameof(Fill)); Raise(nameof(StatusBrush)); Raise(nameof(TickVisibility)); Raise(nameof(Weight)); } }
    }

    bool _paused;
    public bool Paused { get => _paused; set { if (Set(ref _paused, value)) { Raise(nameof(Ring)); Raise(nameof(StatusBrush)); } } }

    Brush RunBrush => Paused ? UI.Warn : UI.Neon;

    public Brush Ring => State switch
    {
        StepState.Done => UI.Ok,
        StepState.Warning => UI.Warn,
        StepState.Error => UI.Err,
        StepState.Running => RunBrush,
        _ => UI.BoxOff,
    };

    public Brush Fill => State switch
    {
        StepState.Done => UI.Ok,
        StepState.Warning => UI.Warn,
        StepState.Error => UI.Err,
        _ => UI.Transparent,
    };

    public Brush StatusBrush => State switch
    {
        StepState.Done => UI.Ok,
        StepState.Warning => UI.Warn,
        StepState.Error => UI.ErrSoft,
        StepState.Running => RunBrush,
        StepState.Unavailable => UI.Label,
        _ => UI.Muted,
    };

    public Visibility TickVisibility => State is StepState.Done or StepState.Warning or StepState.Error ? Visibility.Visible : Visibility.Collapsed;
    public Windows.UI.Text.FontWeight Weight => State == StepState.Running ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Medium;
}

public class LogLine
{
    public string Time { get; init; } = "";
    public string Level { get; init; } = "";
    public string Message { get; init; } = "";
    public string LevelTag => $"[{Level}]";

    public Brush LevelBrush => Level switch
    {
        "OK" => UI.Ok,
        "AVISO" => UI.Warn,
        "ERRO" => UI.ErrSoft,
        _ => UI.Brush("#7fb2ff"),
    };
}
