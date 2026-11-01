using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Assistente pós-formatação: restaura, em ordem, o mais recente de cada parte do backup.</summary>
public sealed partial class WizardPage : Page
{
    record Step(string Key, string Name, string Desc);

    static readonly Step[] Steps =
    [
        new("drv", "Drivers", "Instala os .inf de terceiros (Wi-Fi primeiro, para ter internet)"),
        new("apps", "Programas", "Reinstala pelo winget + lista dos que precisam de instalação manual"),
        new("win", "Configurações do Windows", "Wi-Fi, tema, fontes, impressoras, variáveis, hosts"),
        new("cfg", "Configurações de apps", "Terminal, VS Code, Git… e os presets e plugins de Criadores"),
        new("files", "Arquivos pessoais e saves", "Volta para as pastas originais, incluindo o OneDrive"),
        new("nav", "Navegadores", "Favoritos, configurações e perfis"),
    ];

    readonly RestorePlan _plan = RestorePlan.Latest(Settings.Data.Destination);
    readonly Dictionary<string, bool> _on;

    public WizardPage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 960);
        _on = Steps.ToDictionary(s => s.Key, s => Part(s.Key) != null);

        var dest = string.IsNullOrEmpty(Settings.Data.Destination) ? "[destino]" : Settings.Data.Destination;
        FoundText.Inlines.Add(new Run { Text = _plan.Empty ? "Nenhum backup encontrado em " : "Backups encontrados em " });
        FoundText.Inlines.Add(new Run { Text = dest, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], FontWeight = FontWeights.SemiBold });

        NotifyEnd.IsOn = true;
        NoReboot.Toggled += async (row, on) =>
        {
            if (on && !await Dialogs.NoRebootAsync(XamlRoot)) row.SetQuietly(false);
            Build();
        };
        Build();
    }

    RestorePlan.Part? Part(string key) => key switch
    {
        "drv" => _plan.Drivers,
        "apps" => _plan.Programs,
        "win" => _plan.Windows,
        "cfg" => _plan.Configs,
        "files" => _plan.Files,
        "nav" => _plan.Browsers,
        _ => null,
    };

    static string When(string path) => HomePage.VersionDate(path)?.ToString("dd/MM HH:mm") ?? "";

    static long Bytes(RestorePlan.Part p)
    {
        var keys = p.Folders.Select(f => f.Key).ToHashSet();
        return p.M.Files.Where(f => keys.Contains(f.Folder)).Sum(f => f.Size);
    }

    (string, Microsoft.UI.Xaml.Media.Brush) Note(string key)
    {
        if (Part(key) is not { } p) return ("Sem backup", UI.Label);
        return key switch
        {
            "drv" => ($"{p.M.Drivers.Count} drivers · {When(p.Path)}\n" + (NoReboot.IsOn ? "sem reinício (por sua conta)" : "pede para reiniciar no fim"),
                      NoReboot.IsOn ? UI.Brush("#ff8fa1") : UI.Warn),
            "apps" => ($"{p.M.Programs.Count(x => x.Source is "winget" or "msstore")} programas · {When(p.Path)}\npela internet, via winget", UI.Muted),
            "win" => ($"{p.M.WindowsSettings.Count} itens · {When(p.Path)}", UI.Muted),
            "cfg" => ($"{Sys.Bytes(Bytes(p))} · {When(p.Path)}\nsubstitui as configs padrão", UI.Muted),
            "files" => ($"{Sys.Bytes(Bytes(p))} · {When(p.Path)}\npula o que já existir", UI.Muted),
            "nav" => ($"{p.Folders.Count(f => f.Name != "Lista de perfis" && f.Name != "Senhas exportadas")} perfis · {When(p.Path)}\nfeche os navegadores antes", UI.Warn),
            _ => ("", UI.Muted),
        };
    }

    void Build()
    {
        StepsPanel.Children.Clear();
        var active = 0;
        for (var i = 0; i < Steps.Length; i++)
        {
            var s = Steps[i];
            var available = Part(s.Key) != null;
            var on = _on[s.Key];
            if (on) active++;
            var (note, noteBrush) = Note(s.Key);
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
            var sw = new ToggleButton { Style = (Style)Application.Current.Resources["Switch"], IsChecked = on, IsEnabled = available };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, "Incluir etapa " + s.Name);
            sw.Click += (_, _) => { _on[s.Key] = sw.IsChecked == true; Build(); };
            Grid.SetColumn(sw, 3);
            g.Children.Add(sw);
            StepsPanel.Children.Add(g);
        }
        StepsNote.Text = $"{active} de {Steps.Length} etapas · {(NoReboot.IsOn || !_on["drv"] ? "sem reinício" : "reinicia 1 vez no fim")}";
        EstimateText.Text = Estimate();
    }

    /// <summary>Estimativa grosseira: ~80 MB/s de cópia, ~45 s por programa e uns 5 s por driver.</summary>
    string Estimate()
    {
        double seconds = 0;
        if (_on["drv"] && _plan.Drivers is { } d) seconds += 30 + d.M.Drivers.Count * 5;
        if (_on["apps"] && _plan.Programs is { } pr) seconds += pr.M.Programs.Count(p => p.Source is "winget" or "msstore") * 45;
        if (_on["win"] && _plan.Windows != null) seconds += 20;
        foreach (var key in new[] { "cfg", "files", "nav" })
            if (_on[key] && Part(key) is { } p) seconds += Bytes(p) / (80.0 * 1024 * 1024) + p.Folders.Count * 0.5;
        if (seconds == 0) return "—";
        if (seconds < 60) return "menos de 1 min";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? $"~{(int)ts.TotalHours} h {ts.Minutes:00} min" : $"~{Math.Ceiling(ts.TotalMinutes)} min";
    }

    void OnChange(object sender, RoutedEventArgs e) => App.Shell.Navigate("restore");
    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");

    async void OnStart(object sender, RoutedEventArgs e)
    {
        var sources = _plan.Sources(_on["drv"], _on["apps"], _on["win"], _on["cfg"], _on["files"], _on["nav"]);
        if (sources.Count == 0)
        {
            await Dialogs.InfoAsync(XamlRoot, "Nada para restaurar", "Nenhuma etapa disponível está ligada. Confira o destino dos backups no menu lateral.");
            return;
        }
        if (!Silent.IsOn)
        {
            var parts = new List<string>();
            if (_on["drv"] && _plan.Drivers != null) parts.Add("drivers");
            if (_on["apps"] && _plan.Programs != null) parts.Add("programas (pela internet)");
            if (_on["win"] && _plan.Windows != null) parts.Add("configurações do Windows");
            if (_on["cfg"] && _plan.Configs != null) parts.Add("configurações de apps (substituindo as padrão)");
            if (_on["files"] && _plan.Files != null) parts.Add("arquivos e saves (sem sobrescrever)");
            if (_on["nav"] && _plan.Browsers != null) parts.Add("navegadores (feche-os antes)");
            var ok = await Dialogs.ConfirmAsync(XamlRoot, "Iniciar a restauração completa?",
                $"Vai restaurar {string.Join(", ", parts)} para os locais originais." +
                (sources.Any(x => x.Drivers) && !NoReboot.IsOn ? " No fim, o app pede para reiniciar." : ""),
                "Iniciar", "Agora não");
            if (!ok) return;
        }
        App.Shell.Run(new JobSpec
        {
            Kind = JobKind.Restore, Title = "Pós-formatação", RestoreSources = sources,
            Target = RestoreTarget.Original, Conflict = ConflictMode.Skip, NoReboot = NoReboot.IsOn,
        });
    }
}
