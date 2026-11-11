using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WINbackup.Models;

/// <summary>Pincéis usados no código (os mesmos tokens de Themes/Styles.xaml).</summary>
public static class UI
{
    static readonly Dictionary<string, SolidColorBrush> Cache = new();

    /// <summary>Pincel de uma cor da paleta original, já convertida para o tema atual.</summary>
    public static SolidColorBrush Brush(string hex)
    {
        if (Cache.TryGetValue(hex, out var b)) return b;
        b = new SolidColorBrush(Color(hex));
        Cache[hex] = b;
        return b;
    }

    /// <summary>Cor da paleta original convertida para o tema atual.</summary>
    public static Color Color(string hex) => Services.Theme.Map(ParseColor(hex));

    /// <summary>Atualiza os pincéis já criados depois de trocar o tema.</summary>
    public static void Remap()
    {
        foreach (var (hex, b) in Cache) b.Color = Color(hex);
    }

    public static Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte a = 255;
        if (hex.Length == 8) { a = Convert.ToByte(hex[..2], 16); hex = hex[2..]; }
        return Windows.UI.Color.FromArgb(a, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16));
    }

    public static SolidColorBrush Transparent => new(Colors.Transparent);
    public static SolidColorBrush Text => Brush("#e8f0ff");
    public static SolidColorBrush Nav => Brush("#c9d6ee");
    public static SolidColorBrush Muted => Brush("#8ea3c7");
    public static SolidColorBrush Muted2 => Brush("#a9badb");
    public static SolidColorBrush Label => Brush("#7f93b8");
    public static SolidColorBrush Neon => Brush("#19e3ff");
    public static SolidColorBrush NeonSoft => Brush("#7befff");
    public static SolidColorBrush Blue => Brush("#2f7bff");
    public static SolidColorBrush BlueSoft => Brush("#6fa4ff");
    public static SolidColorBrush Ok => Brush("#3ddc97");
    public static SolidColorBrush Warn => Brush("#ffb547");
    public static SolidColorBrush Err => Brush("#ff3d5a");
    public static SolidColorBrush ErrSoft => Brush("#ff6b81");
    public static SolidColorBrush BoxOff => Brush("#3a4b70");
}
