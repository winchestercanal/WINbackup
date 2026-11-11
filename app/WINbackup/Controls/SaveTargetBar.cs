using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Controls;

/// <summary>Faixa "Salvando em …" das telas de backup. Só exibe o destino escolhido no menu lateral.</summary>
public sealed partial class SaveTargetBar : UserControl
{
    readonly TextBlock _path;

    public SaveTargetBar()
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Icon { Data = Icons.DriveSmall, Width = 16, Height = 16, Foreground = UI.Neon, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = L.T("Salvando em"), FontSize = 12, Foreground = UI.Muted2, VerticalAlignment = VerticalAlignment.Center };
        _path = new TextBlock { FontSize = 12.5, FontFamily = (FontFamily)Application.Current.Resources["Mono"],
            TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        var hint = new TextBlock { Text = L.T("Altere em Destino dos backups"), FontSize = 11.5, Foreground = UI.Muted, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(label, 1);
        Grid.SetColumn(_path, 2);
        Grid.SetColumn(hint, 3);
        grid.Children.Add(icon);
        grid.Children.Add(label);
        grid.Children.Add(_path);
        grid.Children.Add(hint);

        Content = new Border
        {
            Height = 44, Padding = new Thickness(14, 0, 14, 0), CornerRadius = new CornerRadius(8),
            Background = UI.Brush("#0b1a33"), BorderBrush = UI.Brush("#22407a"), BorderThickness = new Thickness(1), Child = grid,
        };
        SizeChanged += (_, e) => hint.Visibility = e.NewSize.Width < 720 ? Visibility.Collapsed : Visibility.Visible;
        Loaded += (_, _) => { Settings.DestinationChanged += Refresh; Refresh(); };
        Unloaded += (_, _) => Settings.DestinationChanged -= Refresh;
    }

    /// <summary>O que vem depois da pasta da versão: "Arquivos", "Drivers"…</summary>
    public string Suffix { get; set; } = "";

    void Refresh()
    {
        var dest = Settings.Data.Destination;
        _path.Text = string.IsNullOrEmpty(dest) ? L.T("Escolha o destino no menu lateral") : Path.Combine(dest, "WINbackup_[DATA]", Suffix);
    }
}
