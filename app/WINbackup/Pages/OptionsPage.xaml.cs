using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Opções. Só a criptografia ainda está marcada como "Em breve".</summary>
public sealed partial class OptionsPage : Page
{
    static readonly (string Comment, string Command)[] Cli =
    [
        ("# backup com um preset", @"WINbackup.exe --preset completo --dest E:\Backup"),
        ("# só programas e configurações", @"WINbackup.exe --only apps,configs --dest E:\Backup"),
        ("# pós-formatação sem perguntas", @"WINbackup.exe --restore E:\Backup --postformat --silent"),
        ("# módulos: files, creators, apps, configs, games, browsers, drivers", @"WINbackup.exe --only files,games --silent"),
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
            else if (on) await Dialogs.InfoAsync(XamlRoot, "Lembrete ligado", "O aviso ao abrir o app já vale. Para avisar também ao entrar no Windows, abra o WINbackup como administrador e ligue de novo.");
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
    }

    void OnCopy(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(string.Join("\r\n", Cli.Select(c => c.Comment + "\r\n" + c.Command)));
        Clipboard.SetContent(dp);
        CopyButton.Content = "Copiado";
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
            await Dialogs.InfoAsync(XamlRoot, on ? "Modo portátil ligado" : "Modo portátil desligado", "Feche e abra o WINbackup para valer.");
        }
        catch (Exception ex)
        {
            row.SetQuietly(!on);
            await Dialogs.InfoAsync(XamlRoot, "Não foi possível mudar", ex.Message);
        }
    }

    /// <summary>Copia o app para um pendrive já em modo portátil, levando as configurações atuais.</summary>
    async void OnMakePortable(object sender, RoutedEventArgs e)
    {
        var folder = FileDialog.PickFolder("Onde criar o WINbackup portátil (ex.: o pendrive)");
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
            await Dialogs.InfoAsync(XamlRoot, "Versão portátil criada", $"Pronto: {target}\\WINbackup.exe. Ela leva suas escolhas atuais e não instala nada no PC onde for aberta.");
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(XamlRoot, "Não foi possível criar", ex.Message);
        }
        finally
        {
            PortableButton.IsEnabled = true;
        }
    }
}
