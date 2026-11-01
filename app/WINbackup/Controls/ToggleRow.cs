using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WINbackup.Models;

namespace WINbackup.Controls;

/// <summary>Interruptor com título e descrição, o padrão de opções do protótipo.</summary>
public sealed partial class ToggleRow : UserControl
{
    readonly ToggleButton _switch;
    readonly TextBlock _title = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _desc = new() { FontSize = 12, Foreground = UI.Muted, TextWrapping = TextWrapping.Wrap };
    bool _silent;

    public event Action<ToggleRow, bool>? Toggled;

    public ToggleRow()
    {
        _switch = new ToggleButton { Style = (Style)Application.Current.Resources["Switch"] };
        _switch.Checked += (_, _) => Changed(true);
        _switch.Unchecked += (_, _) => Changed(false);

        var texts = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(_title);
        texts.Children.Add(_desc);
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(_switch);
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        // A linha inteira é clicável, como no protótipo
        var hit = new Button { Style = (Style)Application.Current.Resources["RowButton"], Content = grid, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left, IsTabStop = false };
        hit.Click += (_, _) => _switch.IsChecked = !(_switch.IsChecked ?? false);
        Content = hit;
    }

    void Changed(bool on)
    {
        if (!_silent) Toggled?.Invoke(this, on);
    }

    public string Title { get => _title.Text; set { _title.Text = value; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_switch, value); } }

    public string Description
    {
        get => _desc.Text;
        set { _desc.Text = value; _desc.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible; }
    }

    public bool IsOn
    {
        get => _switch.IsChecked == true;
        set => _switch.IsChecked = value;
    }

    /// <summary>Muda o estado sem disparar Toggled (para desfazer depois de um "Cancelar").</summary>
    public void SetQuietly(bool on)
    {
        _silent = true;
        _switch.IsChecked = on;
        _silent = false;
    }

    /// <summary>Trilho vermelho quando ligado (opções de risco, como "Não reiniciar").</summary>
    public bool Danger
    {
        set => _switch.BorderBrush = value ? UI.Brush("#d9534f") : UI.Blue;
    }

    public bool Locked
    {
        set => IsEnabled = !value;
    }
}
