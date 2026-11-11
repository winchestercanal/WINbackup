using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace WINbackup.Controls;

/// <summary>
/// Ícone de traço no estilo do protótipo: recebe o atributo "d" de um SVG 24x24
/// e desenha com a cor de Foreground.
/// </summary>
public sealed partial class Icon : UserControl
{
    readonly Path _path = new()
    {
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    public Icon()
    {
        IsTabStop = false;
        Width = 18;
        Height = 18;
        var canvas = new Canvas { Width = 24, Height = 24 };
        canvas.Children.Add(_path);
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        _path.SetBinding(Shape.StrokeProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        _path.StrokeThickness = StrokeThickness;
        Loaded += (_, _) => FollowParent();
        ActualThemeChanged += (_, _) => FollowParent();
    }

    bool _following;

    /// <summary>
    /// Sem Foreground próprio, o ícone pega a cor do ContentPresenter ou controle em volta. Herdar não basta: ao trocar o tema
    /// da janela, o WinUI devolve ao ícone a cor padrão do tema do app em vez da do botão.
    /// </summary>
    void FollowParent()
    {
        if (!_following && ReadLocalValue(ForegroundProperty) != DependencyProperty.UnsetValue) return;
        for (var p = VisualTreeHelper.GetParent(this); p != null; p = VisualTreeHelper.GetParent(p))
        {
            if (p is not (ContentPresenter or Control)) continue;
            SetBinding(ForegroundProperty, new Binding { Source = p, Path = new PropertyPath(nameof(Foreground)) });
            _following = true;
            return;
        }
    }

    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(string), typeof(Icon), new PropertyMetadata(null, (d, e) => ((Icon)d).Apply()));

    public string? Data
    {
        get => (string?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Icon),
        new PropertyMetadata(1.75, (d, e) => ((Icon)d)._path.StrokeThickness = (double)e.NewValue));

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(Icon), new PropertyMetadata(null, (d, e) => ((Icon)d)._path.Fill = (Brush?)e.NewValue));

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    void Apply()
    {
        _path.Data = string.IsNullOrEmpty(Data) ? null : (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), Data);
    }
}
