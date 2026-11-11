using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Opções. Só a criptografia ainda está marcada como "Em breve".</summary>
public sealed partial class OptionsPage : Page
{
    static readonly (string Comment, string Command)[] Cli =
    [
        (L.T("# backup com um preset"), @"WINbackup.exe --preset completo --dest E:\Backup"),
        (L.T("# só programas e configurações"), @"WINbackup.exe --only apps,configs --dest E:\Backup"),
        (L.T("# pós-formatação sem perguntas"), @"WINbackup.exe --restore E:\Backup --postformat --silent"),
        (L.T("# módulos: files, creators, apps, configs, games, browsers, drivers"), @"WINbackup.exe --only files,games --silent"),
    ];

    public OptionsPage()
    {
        InitializeComponent();
        var s = Settings.Data;

        // Criptografia: decisão do HANDOFF é ficar desativada por padrão; ainda não implementada
        Encrypt.IsOn = false;
        Encrypt.Locked = true;
        Remind.IsOn = s.NotifyReminder;
        Remind.Toggled += async (_, on) =>
        {
            s.NotifyReminder = on;
            Settings.Save();
            if (Sys.IsAdmin) Scheduler.SetReminder(on);
            else if (on) await Dialogs.InfoAsync(XamlRoot, L.T("Lembrete ligado"), L.T("O aviso ao abrir o app já vale. Para avisar também ao entrar no Windows, abra o WINbackup como administrador e ligue de novo."));
        };
        Portable.IsOn = Settings.IsPortable;
        Portable.Toggled += async (row, on) => await SetPortable(row, on);

        Hash.IsOn = s.VerifyHash;
        NotifyDone.IsOn = s.NotifyDone;
        NotifyErr.IsOn = s.NotifyErrors;
        Hash.Toggled += (_, on) => { s.VerifyHash = on; Settings.Save(); };
        NotifyDone.Toggled += (_, on) => { s.NotifyDone = on; Settings.Save(); };
        NotifyErr.Toggled += (_, on) => { s.NotifyErrors = on; Settings.Save(); };
        KeepRun.Text = s.KeepVersions.ToString();

        var mono = (FontFamily)Application.Current.Resources["Mono"];
        foreach (var (comment, cmd) in Cli)
        {
            CliLines.Children.Add(new TextBlock { Text = comment, FontFamily = mono, FontSize = 12, LineHeight = 20, Foreground = UI.Label });
            CliLines.Children.Add(new TextBlock { Text = cmd, FontFamily = mono, FontSize = 12, LineHeight = 20, Foreground = UI.NeonSoft, TextWrapping = TextWrapping.Wrap });
        }

        BuildThemes();
        BuildLanguages();
    }

    // ---------- Aparência ----------

    void BuildThemes()
    {
        for (var i = 0; i < Theme.Themes.Length; i++)
        {
            var (code, name) = Theme.Themes[i];
            ThemeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(Swatch(code));
            content.Children.Add(new TextBlock { Text = L.T(name), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            var rb = new RadioButton { Style = (Style)Application.Current.Resources["ChoiceCard"], GroupName = "theme", Content = content, IsChecked = Theme.Current == code };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(rb, L.T(name));
            rb.Checked += (_, _) =>
            {
                if (Theme.Current == code) return;
                var familyChanges = (code == "light") != Theme.IsLight;
                Settings.Data.Theme = code;
                Settings.Save();
                // Claro e escuro usam controles nativos diferentes, escolhidos ao abrir o app
                if (familyChanges && BackupRunner.Current is not { IsFinished: false })
                {
                    App.Shell.Restart();
                    return;
                }
                Theme.Apply(code);
                App.Shell.Reload();
            };
            Grid.SetColumn(rb, i);
            ThemeGrid.Children.Add(rb);
        }
    }

    /// <summary>Miniatura do tema: fundo, menu lateral, cartão e destaque nas cores dele.</summary>
    static Border Swatch(string code)
    {
        SolidColorBrush B(string hex) => new(Theme.Preview(code, hex));
        var grid = new Grid { Height = 54, ColumnSpacing = 0 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new Border { Background = B("#070b15") });
        var main = new StackPanel { Spacing = 5, Padding = new Thickness(7, 8, 7, 0), Background = B("#0a0f1c") };
        main.Children.Add(new Border { Height = 6, Width = 34, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Background = B("#e8f0ff") });
        var card = new Grid { Height = 22, CornerRadius = new CornerRadius(4), Background = B("#0e1526"), BorderBrush = B("#1c2a48"), BorderThickness = new Thickness(1) };
        card.Children.Add(new Border { Width = 18, Height = 8, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Right, Background = B("#19e3ff") });
        main.Children.Add(card);
        Grid.SetColumn(main, 1);
        grid.Children.Add(main);
        return new Border { CornerRadius = new CornerRadius(6), BorderBrush = B("#1c2a48"), BorderThickness = new Thickness(1), Child = grid };
    }

    void BuildLanguages()
    {
        var setting = Settings.Data.Language;
        var options = new List<(string Code, string Name)> { ("auto", L.F("Automático ({0})", L.Languages.First(l => l.Code == L.SystemLanguage).Name)) };
        options.AddRange(L.Languages);
        foreach (var (code, name) in options)
        {
            LangGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var rb = new RadioButton { Style = (Style)Application.Current.Resources["ChoiceCard"], GroupName = "lang",
                Content = new TextBlock { Text = name, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }, IsChecked = setting == code || (code == "auto" && !L.Languages.Any(l => l.Code == setting)) };
            rb.Checked += async (_, _) =>
            {
                if (Settings.Data.Language == code) return;
                Settings.Data.Language = code;
                Settings.Save();
                var next = code == "auto" ? L.SystemLanguage : code;
                if (next == L.Current) return;
                if (BackupRunner.Current is { IsFinished: false })
                {
                    await Dialogs.InfoAsync(XamlRoot, L.T("Idioma salvo"), L.T("Há uma tarefa em andamento. O novo idioma vale na próxima vez que abrir o WINbackup."));
                    return;
                }
                App.Shell.Restart();
            };
            Grid.SetColumn(rb, LangGrid.Children.Count);
            LangGrid.Children.Add(rb);
        }
    }

    void OnCopy(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(string.Join("\r\n", Cli.Select(c => c.Comment + "\r\n" + c.Command)));
        Clipboard.SetContent(dp);
        CopyButton.Content = L.T("Copiado");
    }

    void OnHistory(object sender, RoutedEventArgs e) => App.Shell.Navigate("history");
    /// <summary>Liga/desliga o modo portátil: um arquivo portable.txt ao lado do .exe faz as configurações ficarem em Dados\.</summary>
    async Task SetPortable(ToggleRow row, bool on)
    {
        var marker = Path.Combine(AppContext.BaseDirectory, Settings.PortableMarker);
        try
        {
            Settings.Save();
            if (on)
            {
                var data = Path.Combine(AppContext.BaseDirectory, "Dados");
                Directory.CreateDirectory(data);
                File.Copy(Path.Combine(Settings.Folder, "settings.json"), Path.Combine(data, "settings.json"), true);
                File.WriteAllText(marker, "WINbackup em modo portátil: as configurações ficam na pasta Dados ao lado do .exe.");
            }
            else if (File.Exists(marker))
            {
                File.Delete(marker);
            }
            await Dialogs.InfoAsync(XamlRoot, on ? L.T("Modo portátil ligado") : L.T("Modo portátil desligado"), L.T("Feche e abra o WINbackup para valer."));
        }
        catch (Exception ex)
        {
            row.SetQuietly(!on);
            await Dialogs.InfoAsync(XamlRoot, L.T("Não foi possível mudar"), ex.Message);
        }
    }

    /// <summary>Copia o app para um pendrive já em modo portátil, levando as configurações atuais.</summary>
    async void OnMakePortable(object sender, RoutedEventArgs e)
    {
        var folder = FileDialog.PickFolder(L.T("Onde criar o WINbackup portátil (ex.: o pendrive)"));
        if (folder == null) return;
        var target = Path.Combine(folder, "WINbackup");
        PortableButton.IsEnabled = false;
        try
        {
            Settings.Save();
            await Task.Run(() =>
            {
                var source = AppContext.BaseDirectory.TrimEnd('\\');
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(source, file);
                    if (rel.StartsWith("Dados\\", StringComparison.OrdinalIgnoreCase)) continue;
                    var dest = Path.Combine(target, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, true);
                }
                Directory.CreateDirectory(Path.Combine(target, "Dados"));
                File.Copy(Path.Combine(Settings.Folder, "settings.json"), Path.Combine(target, "Dados", "settings.json"), true);
                File.WriteAllText(Path.Combine(target, Settings.PortableMarker), "WINbackup em modo portátil: as configurações ficam na pasta Dados ao lado do .exe.");
            });
            await Dialogs.InfoAsync(XamlRoot, L.T("Versão portátil criada"), L.F("Pronto: {0}\\WINbackup.exe. Ela leva suas escolhas atuais e não instala nada no PC onde for aberta.", target));
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Não foi possível criar"), ex.Message);
        }
        finally
        {
            PortableButton.IsEnabled = true;
        }
    }
}
