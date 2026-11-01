using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WINbackup.Controls;
using WINbackup.Models;

namespace WINbackup.Pages;

/// <summary>Prévia do módulo Navegadores. Dados de exemplo do protótipo.</summary>
public sealed partial class BrowsersPage : Page
{
    record Profile(string Name, string Account, string Folder);
    record Browser(string Name, Profile[] Profiles, string[] Items);

    static readonly Browser[] Sample =
    [
        new("Microsoft Edge", [new("Pessoal", "[conta Microsoft]", "Default")], ["Favoritos", "Extensões", "Configurações"]),
        new("Google Chrome", [new("Pessoal", "[conta Google]", "Default"), new("Trabalho", "[conta Google do trabalho]", "Profile 1"), new("Canal", "[conta do canal]", "Profile 2")], ["Favoritos", "Extensões", "Configurações"]),
        new("Mozilla Firefox", [new("default-release", "Sem conta Firefox", "[id].default-release")], ["Favoritos", "Extensões", "about:config"]),
        new("Brave", [new("Pessoal", "Perfil local", "Default")], ["Favoritos", "Extensões", "Configurações"]),
        new("Opera", [], ["Favoritos", "Extensões"]),
        new("Vivaldi", [new("Pessoal", "[conta Vivaldi]", "Default"), new("Testes", "Perfil local", "Profile 1")], ["Favoritos", "Speed Dial", "Extensões", "Configurações"]),
    ];

    static readonly string[] AvatarColors = ["#19e3ff", "#6fa4ff", "#ffb547", "#3ddc97"];

    public BrowsersPage()
    {
        InitializeComponent();
        foreach (var b in Sample) Cards.Children.Add(Card(b));
        Cards.SizeChanged += (_, e) => Place(e.NewSize.Width >= 900 ? 3 : 2);
        foreach (var (name, url) in new[] { ("Edge", "edge://settings/autofill/passwords"), ("Chrome", "chrome://password-manager/settings"),
                                             ("Firefox", "about:logins"), ("Brave", "brave://password-manager/settings") })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = $"Abrir no {name}", FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = url, FontSize = 11.5, Foreground = UI.Muted, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center });
            var b = new Button { Style = S("SmallButton"), Height = 34, Padding = new Thickness(12, 0, 12, 0), Content = row };
            b.Click += OnSoon;
            PwButtons.Children.Add(b);
        }
    }

    void Place(int cols)
    {
        Cards.ColumnDefinitions[2].Width = cols == 3 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Cards.ColumnSpacing = 14;
        for (var i = 0; i < Cards.Children.Count; i++)
        {
            Grid.SetColumn((FrameworkElement)Cards.Children[i], i % cols);
            Grid.SetRow((FrameworkElement)Cards.Children[i], i / cols);
        }
    }

    static Style S(string key) => (Style)Application.Current.Resources[key];
    static FontFamily Mono => (FontFamily)Application.Current.Resources["Mono"];

    Border Card(Browser b)
    {
        var installed = b.Profiles.Length > 0;
        var body = new StackPanel { Spacing = 12 };
        var border = new Border { Style = S("Card"), Padding = new Thickness(18, 16, 18, 16), Opacity = installed ? 1 : 0.55,
            BorderBrush = installed ? UI.Brush("#22407a") : UI.Brush("#1c2a48"), Child = body };

        var head = new Grid { ColumnSpacing = 12 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = UI.Brush("#12203d"),
            Child = new Icon { Data = Icons.Browsers, Foreground = UI.BlueSoft } });
        var titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = b.Name, FontSize = 15, FontWeight = FontWeights.SemiBold });
        titles.Children.Add(new TextBlock { FontSize = 12, Foreground = installed ? UI.Muted : UI.Label,
            Text = installed ? $"Instalado · {b.Profiles.Length} {(b.Profiles.Length > 1 ? "perfis" : "perfil")}" : "Não instalado" });
        Grid.SetColumn(titles, 1);
        head.Children.Add(titles);
        body.Children.Add(head);

        var chips = new WrapPanel { Spacing = 6, LineSpacing = 6 };
        var profiles = new List<ToggleButton>();
        if (installed)
        {
            var sw = new ToggleButton { Style = S("Switch"), IsChecked = true };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, "Incluir " + b.Name);
            Grid.SetColumn(sw, 2);
            head.Children.Add(sw);

            var list = new StackPanel { Spacing = 6 };
            list.Children.Add(new TextBlock { Text = "PERFIS ENCONTRADOS", FontSize = 11, FontWeight = FontWeights.Bold, CharacterSpacing = 90, Foreground = UI.Label });
            for (var i = 0; i < b.Profiles.Length; i++)
            {
                var p = b.Profiles[i];
                var g = new Grid { ColumnSpacing = 10 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = UI.Brush(AvatarColors[i % 4]),
                    Child = new TextBlock { Text = p.Name[..1].ToUpperInvariant(), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = UI.Brush("#04121a"),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
                var t = new StackPanel { Spacing = 1 };
                t.Children.Add(new TextBlock { Text = p.Name, FontSize = 13, FontWeight = FontWeights.SemiBold });
                t.Children.Add(new TextBlock { Text = p.Account, FontSize = 11.5, Foreground = UI.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(t, 1);
                g.Children.Add(t);
                var folder = new TextBlock { Text = p.Folder, FontSize = 11, Foreground = UI.Label, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(folder, 2);
                g.Children.Add(folder);
                var tb = new ToggleButton { Style = S("CheckCard"), IsChecked = true, Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(8),
                    Background = UI.Brush("#0a1120"), Content = g };
                profiles.Add(tb);
                list.Children.Add(tb);
            }
            body.Children.Add(list);
            sw.Checked += (_, _) => { foreach (var p in profiles) p.IsChecked = true; Chips(chips, b.Items, true); border.BorderBrush = UI.Brush("#22407a"); };
            sw.Unchecked += (_, _) => { foreach (var p in profiles) p.IsChecked = false; Chips(chips, b.Items, false); border.BorderBrush = UI.Brush("#1c2a48"); };
        }
        Chips(chips, b.Items, installed);
        body.Children.Add(chips);
        return border;
    }

    static void Chips(WrapPanel panel, string[] items, bool on)
    {
        panel.Children.Clear();
        foreach (var label in items)
            panel.Children.Add(new Border
            {
                Padding = new Thickness(10, 3, 10, 3), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
                Background = UI.Brush(on ? "#0b2a2e" : "#151b2b"), BorderBrush = UI.Brush(on ? "#17616a" : "#2a3350"),
                Child = new TextBlock { Text = label, FontSize = 12, Foreground = on ? UI.NeonSoft : UI.Muted },
            });
    }

    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Navegadores");
}
