using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Controls;

/// <summary>
/// Menu lateral: Início, categorias recolhíveis (recolhidas por padrão, a da tela atual abre sozinha),
/// cartão do destino dos backups, Opções e o tutorial.
/// </summary>
public sealed partial class Sidebar : UserControl
{
    record Item(string Key, string Label, string Icon);
    record Group(string Key, string Label, string Icon, Item[] Items);

    static readonly Group[] Groups =
    [
        new("backup", "Backup", Icons.Download,
        [
            new("files", "Arquivos pessoais", Icons.Folder), new("creators", "Criadores", Icons.Creator), new("apps", "Programas", Icons.Apps),
            new("configs", "Configurações", Icons.Configs), new("games", "Saves de jogos", Icons.Games),
            new("drivers", "Drivers", Icons.Drivers), new("browsers", "Navegadores", Icons.Browsers),
        ]),
        new("restaurar", "Restaurar", Icons.Restore,
        [
            new("restore", "Restaurar backup", Icons.Restore), new("wizard", "Pós-formatação", Icons.Wizard),
        ]),
        new("auto", "Painel", Icons.Gauge,
        [
            new("activity", "Atividade", Icons.Activity), new("schedule", "Agendamentos", Icons.Clock),
            new("history", "Histórico e versões", Icons.History),
        ]),
    ];

    readonly Dictionary<string, bool> _open = new();
    string _active = "home";

    public event Action<string>? Navigate;

    public Sidebar()
    {
        InitializeComponent();
        Settings.DestinationChanged += RefreshDestination;
        Loaded += (_, _) => RefreshDestination();
        Build();
    }

    public string Active
    {
        get => _active;
        set
        {
            _active = value;
            Build();
        }
    }

    void Build()
    {
        NavPanel.Children.Clear();
        NavPanel.Children.Add(NavButton("home", "Início", Icons.Home, top: true));
        foreach (var g in Groups)
        {
            var hasActive = g.Items.Any(i => i.Key == _active);
            var open = _open.TryGetValue(g.Key, out var o) ? o : hasActive;

            var chevron = new Icon { Width = 14, Height = 14, StrokeThickness = 2, Data = Icons.Chevron, Foreground = UI.Muted,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5), RenderTransform = new RotateTransform { Angle = open ? 90 : 0 } };
            var header = new Grid { ColumnSpacing = 12 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new Icon { Data = g.Icon });
            var title = new TextBlock { Text = g.Label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center,
                FontWeight = hasActive ? FontWeights.SemiBold : FontWeights.Medium };
            Grid.SetColumn(title, 1);
            header.Children.Add(title);
            Grid.SetColumn(chevron, 2);
            header.Children.Add(chevron);

            var btn = new Button { Style = (Style)Application.Current.Resources["RowButton"], Height = 38, Padding = new Thickness(12, 0, 12, 0),
                Content = header, Foreground = hasActive ? UI.Brush("#ffffff") : UI.Nav };
            AutomationPropertiesHelper(btn, g.Label, open);
            btn.Click += (_, _) => { _open[g.Key] = !open; Build(); };
            NavPanel.Children.Add(btn);

            if (!open) continue;
            var list = new StackPanel { Spacing = 2, Margin = new Thickness(21, 0, 0, 4), Padding = new Thickness(8, 0, 0, 0),
                BorderBrush = UI.Brush("#1a2847"), BorderThickness = new Thickness(1, 0, 0, 0) };
            foreach (var i in g.Items) list.Children.Add(NavButton(i.Key, i.Label, i.Icon, top: false));
            NavPanel.Children.Add(list);
        }

        var don = _active == "donate";
        DonateButton.Background = don ? UI.Brush("#12203d") : UI.Transparent;
        DonatePill.Background = don ? UI.Neon : UI.Transparent;

        var opt = _active == "options";
        OptionsButton.Background = opt ? UI.Brush("#12203d") : UI.Transparent;
        OptionsButton.Foreground = opt ? UI.Brush("#ffffff") : UI.Muted2;
        OptionsPill.Background = opt ? UI.Neon : UI.Transparent;
    }

    Button NavButton(string key, string label, string icon, bool top)
    {
        var on = key == _active;
        var grid = new Grid();
        grid.Children.Add(new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(top ? -12 : -19, 0, 0, 0), Background = on ? UI.Neon : UI.Transparent });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = top ? 12 : 10 };
        row.Children.Add(new Icon { Data = icon, Width = top ? 18 : 16, Height = top ? 18 : 16 });
        row.Children.Add(new TextBlock { Text = label, FontSize = top ? 14 : 13.5, VerticalAlignment = VerticalAlignment.Center,
            FontWeight = on ? FontWeights.SemiBold : top ? FontWeights.Medium : FontWeights.Normal });
        grid.Children.Add(row);

        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["RowButton"],
            Height = top ? 38 : 34,
            Padding = new Thickness(top ? 12 : 10, 0, 10, 0),
            Background = on ? UI.Brush("#12203d") : UI.Transparent,
            Foreground = on ? UI.Brush("#ffffff") : top ? UI.Nav : UI.Muted2,
            Content = grid,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, label);
        btn.Click += (_, _) => Navigate?.Invoke(key);
        return btn;
    }

    static void AutomationPropertiesHelper(Button b, string name, bool open) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, $"{name} ({(open ? "aberto" : "recolhido")})");

    // ---------- Destino ----------

    void RefreshDestination()
    {
        var path = Settings.Data.Destination;
        var d = Destinations.Describe(path);
        DestIcon.Data = d.Icon;
        DestLabel.Text = d.Label;
        DestPath.Text = d.Path;
        var space = Destinations.Space(path);
        if (space is { } s && s.Total > 0)
        {
            var used = (double)(s.Total - s.Free) / s.Total;
            UsedCol.Width = new GridLength(used, GridUnitType.Star);
            FreeCol.Width = new GridLength(1 - used, GridUnitType.Star);
            DestSpace.Text = $"{Sys.Bytes(s.Free)} livres de {Sys.Bytes(s.Total)}";
            DestDot.Fill = UI.Ok;
        }
        else
        {
            UsedCol.Width = new GridLength(0, GridUnitType.Star);
            DestSpace.Text = string.IsNullOrEmpty(path) ? "Clique para escolher" : "Destino indisponível";
            DestDot.Fill = UI.Warn;
        }
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(DestButton, $"Destino dos backups: {d.Path}. Clique para alterar");
    }

    void OnDestOpening(object? sender, object e)
    {
        DestList.Children.Clear();
        var current = Settings.Data.Destination;
        var options = Destinations.Candidates();
        if (!string.IsNullOrEmpty(current) && !options.Any(o => string.Equals(o.Path, current, StringComparison.OrdinalIgnoreCase)))
            options.Insert(0, Destinations.Describe(current));
        if (options.Count == 0)
            DestList.Children.Add(new TextBlock { Text = "Nenhum outro disco encontrado. Conecte um HD externo ou escolha uma pasta.",
                FontSize = 12, Foreground = UI.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 4, 8, 4) });

        foreach (var o in options)
        {
            var on = string.Equals(o.Path, current, StringComparison.OrdinalIgnoreCase);
            var g = new Grid { ColumnSpacing = 10 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Icon { Data = o.Icon, Width = 16, Height = 16, Foreground = on ? UI.Neon : UI.BlueSoft, VerticalAlignment = VerticalAlignment.Center });
            var texts = new StackPanel { Spacing = 1 };
            texts.Children.Add(new TextBlock { Text = o.Label, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            texts.Children.Add(new TextBlock { Text = o.Path, FontSize = 11, Foreground = UI.Muted, FontFamily = (FontFamily)Application.Current.Resources["Mono"], TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(texts, 1);
            g.Children.Add(texts);
            if (on)
            {
                var tick = new Icon { Data = Icons.Check, Width = 14, Height = 14, StrokeThickness = 2.5, Foreground = UI.Neon, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(tick, 2);
                g.Children.Add(tick);
            }
            var b = new Button { Style = (Style)Application.Current.Resources["RowButton"], Padding = new Thickness(8), CornerRadius = new CornerRadius(7),
                Background = on ? UI.Brush("#12203d") : UI.Transparent, Content = g };
            b.Click += (_, _) => { DestFlyout.Hide(); Settings.SetDestination(o.Path); };
            DestList.Children.Add(b);
        }
    }

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        DestFlyout.Hide();
        var folder = FileDialog.PickFolder("Onde salvar os backups?");
        if (folder != null) Settings.SetDestination(folder);
    }

    void OnOptions(object sender, RoutedEventArgs e) => Navigate?.Invoke("options");
    void OnDonate(object sender, RoutedEventArgs e) => Navigate?.Invoke("donate");

    // Link do vídeo ainda a definir (HANDOFF, pendências): por enquanto abre o canal
    void OnYouTube(object sender, RoutedEventArgs e) => Sys.Open("https://www.youtube.com/WINchesterCanal");
}
