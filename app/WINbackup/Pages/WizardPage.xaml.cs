using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Prévia do assistente pós-formatação: 6 etapas em ordem, com o fluxo do "Não reiniciar".</summary>
public sealed partial class WizardPage : Page
{
    record Step(string Key, string Name, string Desc);

    static readonly Step[] Steps =
    [
        new("drv", "Drivers", "Instala os .inf de terceiros (Wi-Fi primeiro, para ter internet)"),
        new("apps", "Programas", "winget import + lista dos que precisam de instalação manual"),
        new("win", "Configurações do Windows", "Wi-Fi, tema, fontes, impressoras, variáveis, hosts"),
        new("cfg", "Configurações de apps", "Terminal, PowerShell, VS Code, Git, OBS, Komorebi…"),
        new("files", "Arquivos pessoais e saves", "Volta para as pastas originais, incluindo o OneDrive"),
        new("nav", "Navegadores", "Favoritos, Speed Dial e lista de extensões"),
    ];

    readonly Dictionary<string, bool> _on = Steps.ToDictionary(s => s.Key, _ => true);

    public WizardPage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 960);
        var last = Destinations.Versions().FirstOrDefault();
        FoundText.Inlines.Add(new Run { Text = last == null ? "Nenhum backup encontrado em " : "Backup encontrado em " });
        FoundText.Inlines.Add(new Run { Text = last ?? (string.IsNullOrEmpty(Settings.Data.Destination) ? "[destino]" : Settings.Data.Destination),
            FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], FontWeight = FontWeights.SemiBold });
        if (last != null) FoundText.Inlines.Add(new Run { Text = " · " + (Backup.Manifest.Load(last)?.Kind ?? "Completo") });

        NotifyEnd.IsOn = true;
        NoReboot.Toggled += async (row, on) =>
        {
            if (on && !await Dialogs.NoRebootAsync(XamlRoot)) row.SetQuietly(false);
            Build();
        };
        Build();
    }

    void Build()
    {
        StepsPanel.Children.Clear();
        var active = 0;
        for (var i = 0; i < Steps.Length; i++)
        {
            var s = Steps[i];
            var on = _on[s.Key];
            if (on) active++;
            var (note, noteBrush) = s.Key switch
            {
                "drv" => NoReboot.IsOn ? ("Sem reinício (por sua conta)", UI.Brush("#ff8fa1")) : ("Reinicia e continua", UI.Warn),
                "apps" => ("[N] programas", UI.Muted),
                "cfg" => ("Depois dos programas", UI.Muted),
                "files" => ("[TAMANHO]", UI.Muted),
                "nav" => ("Fecha os navegadores", UI.Warn),
                _ => ("", UI.Muted),
            };
            var g = new Grid { ColumnSpacing = 16, Padding = new Thickness(18, 16, 18, 16), Opacity = on ? 1 : 0.5,
                BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Border
            {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1.5),
                BorderBrush = on ? UI.Neon : UI.BoxOff, Background = on ? UI.Brush("#0a2240") : UI.Transparent,
                Child = new TextBlock { Text = (i + 1).ToString(), FontWeight = FontWeights.Bold, Foreground = on ? UI.Neon : UI.Muted,
                    FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            });
            var t = new StackPanel { Spacing = 3 };
            t.Children.Add(new TextBlock { Text = s.Name, FontSize = 15, FontWeight = FontWeights.SemiBold });
            t.Children.Add(new TextBlock { Text = s.Desc, FontSize = 12, Foreground = UI.Muted, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var n = new TextBlock { Text = note, FontSize = 12, Foreground = noteBrush, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
            Grid.SetColumn(n, 2);
            g.Children.Add(n);
            var sw = new ToggleButton { Style = (Style)Application.Current.Resources["Switch"], IsChecked = on };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, "Incluir etapa " + s.Name);
            sw.Click += (_, _) => { _on[s.Key] = sw.IsChecked == true; Build(); };
            Grid.SetColumn(sw, 3);
            g.Children.Add(sw);
            StepsPanel.Children.Add(g);
        }
        StepsNote.Text = $"{active} de {Steps.Length} etapas · {(NoReboot.IsOn ? "sem reinício automático" : "reinicia 1 vez")}";
    }

    void OnChange(object sender, RoutedEventArgs e) => App.Shell.Navigate("restore");
    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");
    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Pós-formatação");
}
