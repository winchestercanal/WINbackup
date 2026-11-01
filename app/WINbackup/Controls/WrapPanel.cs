using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace WINbackup.Controls;

/// <summary>Painel que quebra linha (chips de exclusão, etiquetas).</summary>
public sealed partial class WrapPanel : Panel
{
    public double Spacing { get; set; } = 8;
    public double LineSpacing { get; set; } = 8;

    protected override Size MeasureOverride(Size available)
    {
        double x = 0, y = 0, line = 0, width = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(available.Width, double.PositiveInfinity));
            var s = child.DesiredSize;
            if (x > 0 && x + s.Width > available.Width)
            {
                y += line + LineSpacing;
                x = 0;
                line = 0;
            }
            x += s.Width + Spacing;
            line = Math.Max(line, s.Height);
            width = Math.Max(width, x - Spacing);
        }
        return new Size(double.IsInfinity(available.Width) ? width : available.Width, Children.Count == 0 ? 0 : y + line);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double x = 0, y = 0, line = 0;
        foreach (var child in Children)
        {
            var s = child.DesiredSize;
            if (x > 0 && x + s.Width > final.Width)
            {
                y += line + LineSpacing;
                x = 0;
                line = 0;
            }
            child.Arrange(new Rect(x, y, s.Width, s.Height));
            x += s.Width + Spacing;
            line = Math.Max(line, s.Height);
        }
        return final;
    }
}
