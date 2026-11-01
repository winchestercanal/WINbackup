using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Opções. SHA-256 e notificações já valem; o resto está marcado como "Em breve".</summary>
public sealed partial class OptionsPage : Page
{
    static readonly (string Comment, string Command)[] Cli =
    [
        ("# backup com um preset", @"WINbackup.exe --preset completo --dest E:\Backup"),
        ("# só programas e configurações", @"WINbackup.exe --only apps,configs --dest E:\Backup"),
        ("# pós-formatação sem perguntas", @"WINbackup.exe --restore E:\Backup --postformat --silent"),
    ];

    public OptionsPage()
    {
        InitializeComponent();
        var s = Settings.Data;

        // Criptografia: decisão do HANDOFF é ficar desativada por padrão; ainda não implementada
        Encrypt.IsOn = false;
        Encrypt.Locked = true;
        Remind.Locked = true;
        Portable.Locked = true;
        Tray.Locked = true;

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
    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Modo portátil");
}
