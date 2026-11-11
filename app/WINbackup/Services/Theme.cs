using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WINbackup.Models;

namespace WINbackup.Services;

/// <summary>
/// Temas do app. As cores são escritas na paleta original (WINchester, azul-escuro com ciano) e cada tema
/// troca cada cor por outra. Os pincéis são compartilhados (StaticResource e o cache de UI.Brush), então
/// mudar a cor deles atualiza a tela inteira sem reiniciar.
/// </summary>
public static class Theme
{
    public static readonly (string Code, string Name)[] Themes =
    [
        ("light", "Claro"), ("neutral", "Escuro"), ("oled", "Escuro OLED"), ("winchester", "Winchester"),
    ];

    public static string Current { get; private set; } = "winchester";
    public static bool IsLight => Current == "light";
    public static ElementTheme Element => IsLight ? ElementTheme.Light : ElementTheme.Dark;

    /// <summary>Disparado depois de trocar as cores.</summary>
    public static event Action? Changed;

    static Dictionary<string, string> _map = new();
    static readonly Dictionary<SolidColorBrush, Color> Brushes = new(ReferenceEqualityComparer.Instance);
    static readonly Dictionary<GradientStop, Color> Stops = new(ReferenceEqualityComparer.Instance);
    static readonly Dictionary<(ResourceDictionary, object), Color> Colors = new();

    public static void Apply(string code)
    {
        Current = code is "neutral" or "oled" or "light" ? code : "winchester";
        _map = Current switch
        {
            "neutral" => Neutral,
            "oled" => Oled,
            "light" => Light,
            _ => new(),
        };
        Walk(Application.Current.Resources);
        UI.Remap();
        Changed?.Invoke();
    }

    /// <summary>Cor da paleta original convertida para o tema atual.</summary>
    public static Color Map(Color c)
    {
        if (_map.Count == 0) return c;
        var key = c.A == 255 ? $"#{c.R:x2}{c.G:x2}{c.B:x2}" : $"#{c.A:x2}{c.R:x2}{c.G:x2}{c.B:x2}";
        return _map.TryGetValue(key, out var hex) ? UI.ParseColor(hex) : c;
    }

    /// <summary>Cor da paleta original como fica em outro tema (amostras em Opções).</summary>
    public static Color Preview(string code, string hex)
    {
        var map = code switch { "neutral" => Neutral, "oled" => Oled, "light" => Light, _ => null };
        return UI.ParseColor(map != null && map.TryGetValue(hex, out var t) ? t : hex);
    }

    static void Walk(ResourceDictionary d)
    {
        foreach (var md in d.MergedDictionaries) Walk(md);
        foreach (var td in d.ThemeDictionaries.Values.OfType<ResourceDictionary>()) Walk(td);
        foreach (var key in d.Keys.ToList())
        {
            switch (d[key])
            {
                case SolidColorBrush b:
                    Remap(b);
                    break;
                case GradientBrush g:
                    foreach (var stop in g.GradientStops)
                    {
                        if (!Stops.TryGetValue(stop, out var o)) Stops[stop] = o = stop.Color;
                        stop.Color = Map(o);
                    }
                    break;
                case Color c:
                    if (!Colors.TryGetValue((d, key), out var oc)) Colors[(d, key)] = oc = c;
                    d[key] = Map(oc);
                    break;
                case Style s:
                    foreach (var setter in s.Setters.OfType<Setter>())
                        if (setter.Value is SolidColorBrush sb) Remap(sb);
                    break;
            }
        }
    }

    static void Remap(SolidColorBrush b)
    {
        if (!Brushes.TryGetValue(b, out var original)) Brushes[b] = original = b.Color;
        b.Color = Map(original);
    }

    // ---------- Paletas: cor original → cor do tema ----------

    /// <summary>Escuro, no estilo do Windows 11: cinzas no lugar do azul-marinho e azul do Windows no destaque.</summary>
    static readonly Dictionary<string, string> Neutral = new()
    {
        // fundos
        ["#0a0f1c"] = "#202020", ["#070b15"] = "#1b1b1b", ["#080d19"] = "#1d1d1d", ["#0a1120"] = "#1c1c1c",
        // superfícies
        ["#0e1526"] = "#2b2b2b", ["#0e1830"] = "#2d2d2d", ["#0f1b33"] = "#323232", ["#0b1a33"] = "#2a2a2a", ["#101b33"] = "#2d2d2d",
        ["#0c1426"] = "#272727", ["#0b1222"] = "#262626", ["#0f1d36"] = "#2b2b2b",
        // selecionado
        ["#12203d"] = "#383838", ["#1a2b52"] = "#454545", ["#0a2240"] = "#353535", ["#13213f"] = "#3a3a3a",
        // linhas
        ["#141f38"] = "#2b2b2b", ["#1c2a48"] = "#3a3a3a", ["#16223d"] = "#333333", ["#121c33"] = "#303030", ["#1a2847"] = "#3a3a3a",
        ["#22407a"] = "#4d4d4d", ["#2a3b63"] = "#474747", ["#2f4a80"] = "#555555", ["#35508a"] = "#5c5c5c", ["#1f3560"] = "#444444",
        ["#2a3350"] = "#404040", ["#3a4b70"] = "#7a7a7a", ["#151b2b"] = "#2a2a2a", ["#1a2236"] = "#2d2d2d", ["#22304a"] = "#383838",
        // texto
        ["#e8f0ff"] = "#ffffff", ["#c9d6ee"] = "#e0e0e0", ["#d5e1f7"] = "#e6e6e6", ["#a9badb"] = "#c8c8c8", ["#8ea3c7"] = "#a6a6a6",
        ["#7f93b8"] = "#9a9a9a", ["#5f7398"] = "#7a7a7a",
        // destaque
        ["#19e3ff"] = "#60cdff", ["#7befff"] = "#99ebff", ["#04121a"] = "#000000", ["#2f7bff"] = "#4cc2ff", ["#6fa4ff"] = "#76b9ed",
        ["#9fc3ff"] = "#99d9ff", ["#7fb2ff"] = "#88c8f5",
        // fichas ciano
        ["#0b2a2e"] = "#1d3540", ["#0b1f24"] = "#1d2c33", ["#17616a"] = "#3a7186",
    };

    /// <summary>Escuro OLED: fundo preto puro, superfícies quase pretas e o mesmo destaque ciano.</summary>
    static readonly Dictionary<string, string> Oled = new()
    {
        ["#0a0f1c"] = "#000000", ["#070b15"] = "#000000", ["#080d19"] = "#0a0a0a", ["#0a1120"] = "#050505",
        ["#0e1526"] = "#0b0d12", ["#0e1830"] = "#0c0f16", ["#0f1b33"] = "#10141d", ["#0b1a33"] = "#0a0e16", ["#101b33"] = "#0e1118",
        ["#0c1426"] = "#0a0c11", ["#0b1222"] = "#0d0f14", ["#0f1d36"] = "#0d1119",
        ["#12203d"] = "#141a28", ["#1a2b52"] = "#1c2438", ["#0a2240"] = "#0e1a30", ["#13213f"] = "#161c2a",
        ["#141f38"] = "#151a24", ["#1c2a48"] = "#1d2433", ["#16223d"] = "#181e2b", ["#121c33"] = "#141922", ["#1a2847"] = "#1d2433",
        ["#22407a"] = "#273550", ["#2a3b63"] = "#283247", ["#2f4a80"] = "#30405e", ["#35508a"] = "#3a4d70", ["#1f3560"] = "#22304a",
        ["#2a3350"] = "#2a3040", ["#3a4b70"] = "#3a4560", ["#151b2b"] = "#11151d", ["#1a2236"] = "#161a22", ["#22304a"] = "#1f2533",
        ["#1d1608"] = "#140f05", ["#2a1f0b"] = "#1d1607", ["#0b2a1f"] = "#071c15", ["#2a0d15"] = "#1d080e", ["#2a0d1f"] = "#1c0814",
        ["#0b2a2e"] = "#071d20", ["#0b1f24"] = "#06151a",
    };

    /// <summary>Claro: fundo cinza-claro, cartões brancos e azul do Windows; cores de aviso mais escuras para ler no branco.</summary>
    static readonly Dictionary<string, string> Light = new()
    {
        ["#0a0f1c"] = "#f3f3f3", ["#070b15"] = "#ebebeb", ["#080d19"] = "#ffffff", ["#0a1120"] = "#f7f7f7",
        ["#0e1526"] = "#ffffff", ["#0e1830"] = "#fbfbfb", ["#0f1b33"] = "#eef4fd", ["#0b1a33"] = "#eaf2fd", ["#101b33"] = "#ffffff",
        ["#0c1426"] = "#ffffff", ["#0b1222"] = "#f9f9f9", ["#0f1d36"] = "#e8f1fd",
        ["#12203d"] = "#e3edfb", ["#1a2b52"] = "#d6e5fa", ["#0a2240"] = "#e0ecfb", ["#13213f"] = "#e3e3e3",
        ["#141f38"] = "#dedede", ["#1c2a48"] = "#e1e1e1", ["#16223d"] = "#ebebeb", ["#121c33"] = "#ededed", ["#1a2847"] = "#e0e0e0",
        ["#22407a"] = "#c3d4ee", ["#2a3b63"] = "#d1d1d1", ["#2f4a80"] = "#b8c6dc", ["#35508a"] = "#a9b8d0", ["#1f3560"] = "#b9cde9",
        ["#2a3350"] = "#c8c8c8", ["#3a4b70"] = "#8a8a8a", ["#151b2b"] = "#f0f0f0", ["#1a2236"] = "#e5e5e5", ["#22304a"] = "#d8d8d8",
        ["#e8f0ff"] = "#1b1b1b", ["#ffffff"] = "#000000", ["#c9d6ee"] = "#2b2b2b", ["#d5e1f7"] = "#262626", ["#a9badb"] = "#4a4a4a",
        ["#8ea3c7"] = "#5d5d5d", ["#7f93b8"] = "#666666", ["#5f7398"] = "#8a8a8a",
        ["#19e3ff"] = "#0067c0", ["#7befff"] = "#005a9e", ["#04121a"] = "#ffffff", ["#2f7bff"] = "#0078d4", ["#6fa4ff"] = "#2b7cd3",
        ["#9fc3ff"] = "#005fb8", ["#7fb2ff"] = "#1f6fc5",
        ["#3ddc97"] = "#0f7b0f", ["#0b2a1f"] = "#dff6dd", ["#ffb547"] = "#9d5d00", ["#1d1608"] = "#fff4ce", ["#2a1f0b"] = "#fff4ce",
        ["#5c4413"] = "#f2d48a", ["#ffd89a"] = "#7a4a00", ["#1a1205"] = "#ffffff",
        ["#ff3d5a"] = "#c42b1c", ["#ff6b81"] = "#c42b1c", ["#2a0d15"] = "#fde7e9", ["#6a2333"] = "#f1b6bc", ["#ff8fa1"] = "#a4262c",
        ["#d9534f"] = "#c42b1c", ["#ff5c6c"] = "#d1343f", ["#ff8fb8"] = "#c2185b", ["#2a0d1f"] = "#fce4ec",
        ["#0b2a2e"] = "#e0f4f8", ["#0b1f24"] = "#e6f6f9", ["#17616a"] = "#9fd8e2",
        ["#b802040a"] = "#66000000",
    };
}
