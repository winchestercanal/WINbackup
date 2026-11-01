using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Models;

namespace WINbackup.Controls;

/// <summary>Aviso nas telas cujo módulo ainda não executa nada nesta versão.</summary>
public sealed partial class PreviewNotice : UserControl
{
    readonly TextBlock _text = new() { FontSize = 12.5, Foreground = UI.Brush("#9fc3ff"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };

    public PreviewNotice()
    {
        Text = "Prévia: esta tela ainda mostra dados de exemplo. O módulo entra numa próxima versão; já funcionam: Arquivos pessoais, Criadores, Drivers e a restauração.";
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new Icon { Data = Icons.Info, Width = 16, Height = 16, Foreground = UI.Brush("#6fa4ff"), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(_text, 1);
        grid.Children.Add(_text);
        Content = new Border
        {
            Padding = new Thickness(14, 10, 14, 10), CornerRadius = new CornerRadius(10),
            Background = UI.Brush("#0c1730"), BorderBrush = UI.Brush("#1f3560"), BorderThickness = new Thickness(1), Child = grid,
        };
    }

    public string Text { get => _text.Text; set => _text.Text = value; }
}
