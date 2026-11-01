using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WINbackup.Controls;

/// <summary>
/// Layout de duas colunas que vira uma coluna só quando a janela fica estreita
/// (o protótipo foi desenhado para 1280 px; monitores verticais ficam bem abaixo disso).
/// </summary>
public static class Responsive
{
    public static void StackWhenNarrow(Grid grid, double minWidth)
    {
        var widths = grid.ColumnDefinitions.Select(c => c.Width).ToList();
        var columns = grid.Children.OfType<FrameworkElement>().ToDictionary(e => e, Grid.GetColumn);
        while (grid.RowDefinitions.Count < widths.Count) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bool? narrow = null;

        grid.SizeChanged += (_, e) =>
        {
            var now = e.NewSize.Width < minWidth;
            if (now == narrow) return;
            narrow = now;
            grid.RowSpacing = now ? grid.ColumnSpacing : 0;
            for (var i = 0; i < widths.Count; i++)
                grid.ColumnDefinitions[i].Width = !now ? widths[i] : i == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            foreach (var (child, col) in columns)
            {
                Grid.SetColumn(child, now ? 0 : col);
                Grid.SetRow(child, now ? col : 0);
            }
        };
    }
}
