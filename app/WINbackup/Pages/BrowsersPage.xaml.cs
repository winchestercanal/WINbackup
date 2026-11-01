using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Navegadores e perfis detectados, exportação guiada de senhas.</summary>
public sealed partial class BrowsersPage : Page
{
    static readonly string[] AvatarColors = ["#19e3ff", "#6fa4ff", "#ffb547", "#3ddc97"];
    readonly List<BrowserInfo> _browsers = BrowsersModule.Detect();

    SettingsData S => Settings.Data;

    public BrowsersPage()
    {
        InitializeComponent();
        foreach (var b in _browsers) Cards.Children.Add(Card(b));
        Cards.SizeChanged += (_, e) => Place(e.NewSize.Width >= 900 ? 3 : 2);

        foreach (var b in _browsers.Where(b => b.Installed))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = $"Abrir no {b.Name.Replace("Microsoft ", "").Replace("Google ", "").Replace("Mozilla ", "")}", FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = b.PasswordUrl, FontSize = 11.5, Foreground = UI.Muted, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center });
            var btn = new Button { Style = S_("SmallButton"), Height = 34, Padding = new Thickness(12, 0, 12, 0), Content = row };
            btn.Click += async (_, _) => await OpenPasswords(b);
            PwButtons.Children.Add(btn);
        }
        if (PwButtons.Children.Count == 0)
            PwButtons.Children.Add(new TextBlock { Text = "Nenhum navegador encontrado.", Foreground = UI.Muted, FontSize = 12.5 });

        LoadCsvs();
        CheckRunning();
        UpdateSave();
    }

    static Style S_(string key) => (Style)Application.Current.Resources[key];
    static FontFamily Mono => (FontFamily)Application.Current.Resources["Mono"];

    void Place(int cols)
    {
        Cards.ColumnDefinitions[2].Width = cols == 3 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        for (var i = 0; i < Cards.Children.Count; i++)
        {
            Grid.SetColumn((FrameworkElement)Cards.Children[i], i % cols);
            Grid.SetRow((FrameworkElement)Cards.Children[i], i / cols);
        }
    }

    // ---------- Cartões ----------

    Border Card(BrowserInfo b)
    {
        var browserOn = !S.BrowsersOff.Contains(b.Key, StringComparer.OrdinalIgnoreCase);
        var body = new StackPanel { Spacing = 12 };
        var border = new Border { Style = S_("Card"), Padding = new Thickness(18, 16, 18, 16), Opacity = b.Installed ? 1 : 0.55, Child = body };

        var head = new Grid { ColumnSpacing = 12 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition());
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = UI.Brush("#12203d"),
            Child = new Icon { Data = Icons.Browsers, Foreground = UI.BlueSoft } });
        var titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = b.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        titles.Children.Add(new TextBlock { FontSize = 12, Foreground = b.Installed ? UI.Muted : UI.Label,
            Text = b.Installed ? $"Instalado · {b.Profiles.Count} {(b.Profiles.Count > 1 ? "perfis" : "perfil")}" : "Não instalado" });
        Grid.SetColumn(titles, 1);
        head.Children.Add(titles);
        body.Children.Add(head);

        var chips = new WrapPanel { Spacing = 6, LineSpacing = 6 };
        var items = new List<string> { b.Key == "vivaldi" ? "Favoritos e Speed Dial" : "Favoritos" };
        var extCount = b.Profiles.Sum(p => p.Extensions);
        items.Add(b.Firefox ? $"Extensões ({extCount})" : $"Lista de extensões ({extCount})");
        items.Add(b.Firefox ? "about:config" : "Configurações");

        if (b.Installed)
        {
            var sw = new ToggleButton { Style = S_("Switch"), IsChecked = browserOn };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, "Incluir " + b.Name);
            Grid.SetColumn(sw, 2);
            head.Children.Add(sw);

            var list = new StackPanel { Spacing = 6 };
            list.Children.Add(new TextBlock { Text = "PERFIS ENCONTRADOS", FontSize = 11, FontWeight = FontWeights.Bold, CharacterSpacing = 90, Foreground = UI.Label });
            var toggles = new List<ToggleButton>();
            for (var i = 0; i < b.Profiles.Count; i++)
            {
                var p = b.Profiles[i];
                var g = new Grid { ColumnSpacing = 10 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = UI.Brush(AvatarColors[i % 4]),
                    Child = new TextBlock { Text = p.Name.Length > 0 ? p.Name[..1].ToUpperInvariant() : "?", FontSize = 12, FontWeight = FontWeights.Bold,
                        Foreground = UI.Brush("#04121a"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
                var t = new StackPanel { Spacing = 1 };
                t.Children.Add(new TextBlock { Text = p.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
                if (p.Account.Length > 0)
                    t.Children.Add(new TextBlock { Text = p.Account, FontSize = 11.5, Foreground = UI.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(t, 1);
                g.Children.Add(t);
                var folder = new TextBlock { Text = p.Folder, FontSize = 11, Foreground = UI.Label, FontFamily = Mono, VerticalAlignment = VerticalAlignment.Center,
                    MaxWidth = 110, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(folder, 2);
                // No Firefox a pasta ("xxxx.default-release") só repete o nome do perfil
                if (!b.Firefox) g.Children.Add(folder);
                var tb = new ToggleButton { Style = S_("CheckCard"), IsChecked = browserOn && BrowsersModule.IsSelected(b, p), IsEnabled = browserOn,
                    Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(8), Background = UI.Brush("#0a1120"), Content = g };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tb, $"{b.Name} · {p.Name}");
                var key = BrowsersModule.Key(b, p);
                tb.Click += (_, _) =>
                {
                    S.BrowsersOff.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                    if (tb.IsChecked != true) S.BrowsersOff.Add(key);
                    Settings.Save();
                    UpdateSave();
                };
                toggles.Add(tb);
                list.Children.Add(tb);
            }
            body.Children.Add(list);
            void SetBrowser(bool on)
            {
                S.BrowsersOff.RemoveAll(k => string.Equals(k, b.Key, StringComparison.OrdinalIgnoreCase));
                if (!on) S.BrowsersOff.Add(b.Key);
                Settings.Save();
                for (var i = 0; i < toggles.Count; i++)
                {
                    toggles[i].IsEnabled = on;
                    toggles[i].IsChecked = on && BrowsersModule.IsSelected(b, b.Profiles[i]);
                }
                Chips(chips, items, on);
                border.BorderBrush = UI.Brush(on ? "#22407a" : "#1c2a48");
                UpdateSave();
            }
            sw.Click += (_, _) => SetBrowser(sw.IsChecked == true);
            border.BorderBrush = UI.Brush(browserOn ? "#22407a" : "#1c2a48");
        }
        Chips(chips, items, b.Installed && browserOn);
        body.Children.Add(chips);
        return border;
    }

    static void Chips(WrapPanel panel, List<string> items, bool on)
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

    void UpdateSave()
    {
        var profiles = _browsers.Sum(b => b.Profiles.Count(p => BrowsersModule.IsSelected(b, p)));
        SaveButton.Content = profiles == 0 ? "Nada selecionado" : profiles == 1 ? "Salvar 1 perfil" : $"Salvar {profiles} perfis";
        SaveButton.IsEnabled = profiles > 0 || S.PasswordCsvs.Count > 0;
    }

    void CheckRunning()
    {
        var running = BrowsersModule.Running(_browsers.Where(b => b.Installed));
        RunningWarn.Visibility = running.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        RunningText.Text = $"{string.Join(", ", running)} {(running.Count > 1 ? "estão abertos" : "está aberto")}. Feche antes de salvar para garantir que favoritos e configurações estejam atualizados no disco.";
    }

    void OnRecheck(object sender, RoutedEventArgs e) => CheckRunning();

    // ---------- Senhas ----------

    async Task OpenPasswords(BrowserInfo b)
    {
        var exe = BrowsersModule.FindExe(b);
        if (exe == null)
        {
            await Dialogs.InfoAsync(XamlRoot, "Navegador não encontrado", $"Abra o {b.Name} e digite na barra de endereço: {b.PasswordUrl}");
            return;
        }
        try { ShellLauncher.Run(exe, b.PasswordUrl); }
        catch (Exception ex) { await Dialogs.InfoAsync(XamlRoot, "Não foi possível abrir", ex.Message); }
    }

    void LoadCsvs()
    {
        CsvList.Children.Clear();
        foreach (var path in S.PasswordCsvs.ToList())
        {
            var g = new Grid { ColumnSpacing = 8 };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = path, FontFamily = Mono, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis, Foreground = File.Exists(path) ? UI.Text : UI.Warn });
            var remove = new Button { Style = S_("SmallButton"), Height = 28, FontSize = 12, Content = "Remover" };
            remove.Click += (_, _) => { S.PasswordCsvs.Remove(path); Settings.Save(); LoadCsvs(); UpdateSave(); };
            Grid.SetColumn(remove, 1);
            g.Children.Add(remove);
            CsvList.Children.Add(g);
        }
        if (S.PasswordCsvs.Count == 0)
            CsvList.Children.Add(new TextBlock { Text = "Nenhum .csv de senhas adicionado.", FontSize = 12.5, Foreground = UI.Muted });
        CsvWarn.Visibility = S.PasswordCsvs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnAddCsv(object sender, RoutedEventArgs e)
    {
        var file = FileDialog.PickFile("Senhas exportadas pelo navegador", "Senhas exportadas (*.csv)", "*.csv");
        if (file == null || S.PasswordCsvs.Contains(file, StringComparer.OrdinalIgnoreCase)) return;
        S.PasswordCsvs.Add(file);
        Settings.Save();
        LoadCsvs();
        UpdateSave();
    }

    // ---------- Salvar ----------

    async void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(S.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, "Escolha o destino", "Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta.");
            return;
        }
        var running = BrowsersModule.Running(_browsers.Where(b => b.Installed));
        if (running.Count > 0 && !await Dialogs.ConfirmAsync(XamlRoot, "Navegador aberto",
                $"{string.Join(", ", running)} {(running.Count > 1 ? "estão abertos" : "está aberto")}. O backup funciona, mas as últimas mudanças podem ficar de fora. Continuar mesmo assim?",
                "Continuar", "Vou fechar"))
            return;
        App.Shell.Run(new JobSpec
        {
            Title = "Navegadores", FilesLabel = "Navegadores", Browsers = true,
            Folders = BrowsersModule.SelectedFolders(_browsers), Exclusions = new(),
        });
    }
}
