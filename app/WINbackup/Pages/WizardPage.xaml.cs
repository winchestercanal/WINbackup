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
        new("apps", "Programas", "winget import + lista dos que precisam de instalação manual"),
        new("win", "Configurações do Windows", "Wi-Fi, tema, fontes, impressoras, variáveis, hosts"),
        new("cfg", "Configurações de apps", "Presets, workspaces e plugins de Adobe, DaVinci, OBS, Stream Deck… (Criadores)"),
        new("files", "Arquivos pessoais e saves", "Volta para as pastas originais, incluindo o OneDrive"),
        new("nav", "Navegadores", "Favoritos, Speed Dial e lista de extensões"),
    ];

    /// <summary>Etapas que ainda não existem nesta versão.</summary>
    static readonly HashSet<string> Soon = ["win"];

    readonly Dictionary<string, bool> _on = Steps.ToDictionary(s => s.Key, s => !Soon.Contains(s.Key));

    // O mais recente de cada parte: versão e pastas
    (string Path, Manifest M, List<ManifestFolder> Folders)? _files, _creators, _browsers;
    (string Path, Manifest M)? _drivers, _programs;

    public WizardPage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 960);
        FindSources();
        var dest = string.IsNullOrEmpty(Settings.Data.Destination) ? "[destino]" : Settings.Data.Destination;
        var found = _files != null || _creators != null || _drivers != null;
        FoundText.Inlines.Add(new Run { Text = found ? "Backups encontrados em " : "Nenhum backup encontrado em " });
        FoundText.Inlines.Add(new Run { Text = dest, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], FontWeight = FontWeights.SemiBold });
        foreach (var key in new[] { "drv", "apps", "cfg", "files", "nav" }) _on[key] = Available(key);

        NotifyEnd.IsOn = true;
        NoReboot.Toggled += async (row, on) =>
        {
            if (on && !await Dialogs.NoRebootAsync(XamlRoot)) row.SetQuietly(false);
            Build();
        };
        Build();
    }

    /// <summary>Versões completas, da mais nova para a mais antiga: a primeira que tiver cada parte ganha.</summary>
    void FindSources()
    {
        foreach (var path in Destinations.Versions())
        {
            var m = Manifest.Load(path);
            if (m is not { Completed: true }) continue;
            var groups = m.Folders.Select(f => (F: f, G: RestoreModule.GroupOf(m, f))).ToList();
            var files = groups.Where(x => x.G == "Arquivos").Select(x => x.F).ToList();
            var creators = groups.Where(x => x.G.StartsWith("Criadores")).Select(x => x.F).ToList();
            if (_files == null && files.Count > 0) _files = (path, m, files);
            if (_creators == null && creators.Count > 0) _creators = (path, m, creators);
            var browsers = groups.Where(x => x.G.StartsWith("Navegadores")).Select(x => x.F).ToList();
            if (_browsers == null && browsers.Count > 0) _browsers = (path, m, browsers);
            if (_programs == null && m.Programs.Any(p => p.Source is "winget" or "msstore")) _programs = (path, m);
            if (_drivers == null && (Directory.Exists(Path.Combine(path, "Drivers")) || File.Exists(Path.Combine(path, "Drivers_Backup.zip")))) _drivers = (path, m);
        }
    }

    static string When(string path) => HomePage.VersionDate(path)?.ToString("dd/MM HH:mm") ?? "";

    static string Size(Manifest m, List<ManifestFolder> folders)
    {
        var keys = folders.Select(f => f.Key).ToHashSet();
        return Sys.Bytes(m.Files.Where(f => keys.Contains(f.Folder)).Sum(f => f.Size));
    }

    bool Available(string key) => key switch
    {
        "drv" => _drivers != null,
        "apps" => _programs != null,
        "cfg" => _creators != null,
        "files" => _files != null,
        "nav" => _browsers != null,
        _ => false,
    };

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
                _ when Soon.Contains(s.Key) => ("Em breve", UI.Label),
                _ when !Available(s.Key) => ("Sem backup", UI.Label),
                "drv" => ($"{_drivers!.Value.M.Drivers.Count} drivers · {When(_drivers.Value.Path)}" + (NoReboot.IsOn ? "\nsem reinício (por sua conta)" : "\npede para reiniciar no fim"),
                          NoReboot.IsOn ? UI.Brush("#ff8fa1") : UI.Warn),
                "apps" => ($"{_programs!.Value.M.Programs.Count(p => p.Source is "winget" or "msstore")} programas · {When(_programs.Value.Path)}\npela internet, via winget", UI.Muted),
                "cfg" => ($"{Size(_creators!.Value.M, _creators.Value.Folders)} · {When(_creators.Value.Path)}\nsubstitui as configs padrão", UI.Muted),
                "nav" => ($"{_browsers!.Value.Folders.Count(f => f.Name != "Lista de perfis" && f.Name != "Senhas exportadas")} perfis · {When(_browsers.Value.Path)}\nfeche os navegadores antes", UI.Warn),
                "files" => ($"{Size(_files!.Value.M, _files.Value.Folders)} · {When(_files.Value.Path)}\npula o que já existir", UI.Muted),
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
            var sw = new ToggleButton { Style = (Style)Application.Current.Resources["Switch"], IsChecked = on, IsEnabled = Available(s.Key) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, "Incluir etapa " + s.Name);
            sw.Click += (_, _) => { _on[s.Key] = sw.IsChecked == true; Build(); };
            Grid.SetColumn(sw, 3);
            g.Children.Add(sw);
            StepsPanel.Children.Add(g);
        }
        StepsNote.Text = $"{active} de {Steps.Length} etapas · {(NoReboot.IsOn || !_on["drv"] ? "sem reinício" : "reinicia 1 vez no fim")}";
        EstimateText.Text = Estimate();
    }

    /// <summary>Estimativa grosseira: ~80 MB/s de cópia e uns 5 s por driver.</summary>
    string Estimate()
    {
        double seconds = 0;
        if (_on["drv"] && _drivers is { } d) seconds += 30 + d.M.Drivers.Count * 5;
        if (_on["apps"] && _programs is { } pr) seconds += pr.M.Programs.Count(p => p.Source is "winget" or "msstore") * 45;
        foreach (var part in new[] { _on["cfg"] ? _creators : null, _on["files"] ? _files : null, _on["nav"] ? _browsers : null })
        {
            if (part is not { } p) continue;
            var keys = p.Folders.Select(f => f.Key).ToHashSet();
            seconds += p.M.Files.Where(f => keys.Contains(f.Folder)).Sum(f => f.Size) / (80.0 * 1024 * 1024) + p.M.Files.Count * 0.002;
        }
        if (seconds == 0) return "—";
        if (seconds < 60) return "menos de 1 min";
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1 ? $"~{(int)t.TotalHours} h {t.Minutes:00} min" : $"~{Math.Ceiling(t.TotalMinutes)} min";
    }

    void OnChange(object sender, RoutedEventArgs e) => App.Shell.Navigate("restore");
    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");
    async void OnStart(object sender, RoutedEventArgs e)
    {
        var sources = new List<RestoreSource>();
        if (_on["drv"] && _drivers is { } d) sources.Add(new RestoreSource(d.Path, d.M, [], null, true));
        if (_on["apps"] && _programs is { } pr) sources.Add(new RestoreSource(pr.Path, pr.M, [], null, false, Programs: true));
        // Configs de programas substituem as padrão criadas na reinstalação; arquivos pessoais nunca são sobrescritos
        if (_on["cfg"] && _creators is { } c) sources.Add(new RestoreSource(c.Path, c.M, c.Folders.Select(f => f.Key).ToHashSet(), null, false, ConflictMode.Overwrite));
        if (_on["files"] && _files is { } f) sources.Add(new RestoreSource(f.Path, f.M, f.Folders.Select(x => x.Key).ToHashSet(), null, false, ConflictMode.Skip));
        // Favoritos e configurações substituem os do navegador recém-instalado
        if (_on["nav"] && _browsers is { } b) sources.Add(new RestoreSource(b.Path, b.M, b.Folders.Select(x => x.Key).ToHashSet(), null, false, ConflictMode.Overwrite));
        if (sources.Count == 0)
        {
            await Dialogs.InfoAsync(XamlRoot, "Nada para restaurar", "Nenhuma etapa disponível está ligada. Confira o destino dos backups no menu lateral.");
            return;
        }
        if (!Silent.IsOn)
        {
            var parts = new List<string>();
            if (sources.Any(x => x.Drivers)) parts.Add("drivers");
            if (sources.Any(x => x.Programs)) parts.Add("programas (pela internet)");
            if (_on["cfg"] && _creators != null) parts.Add("configurações de apps (substituindo as padrão)");
            if (_on["files"] && _files != null) parts.Add("arquivos pessoais (sem sobrescrever)");
            if (_on["nav"] && _browsers != null) parts.Add("navegadores (feche-os antes)");
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
            Unavailable = ["Configurações do Windows"],
        });
    }
}
