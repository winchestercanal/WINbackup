using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Controls;
using WINbackup.Models;

namespace WINbackup.Pages;

/// <summary>Prévia do módulo Configurações. Dados de exemplo do protótipo.</summary>
public sealed partial class ConfigsPage : Page
{
    readonly List<CheckItem> _win, _apps;

    public ConfigsPage()
    {
        InitializeComponent();
        _win = Items(
            ("wifi", "Redes Wi-Fi", "Perfis salvos, com senha", "[N] redes", true),
            ("theme", "Papel de parede e tema", "Fundo, cor de destaque, modo escuro", "", true),
            ("print", "Impressoras", "Filas e drivers de impressão", "[N]", true),
            ("fonts", "Fontes instaladas", "Só as que não vêm com o Windows", "[N] fontes", true),
            ("env", "Variáveis de ambiente", "Usuário e PATH personalizado", "", true),
            ("hosts", "Arquivo hosts", @"C:\Windows\System32\drivers\etc\hosts", "", false));
        _apps = Items(
            ("term", "Windows Terminal", "settings.json", "Detectado", true),
            ("pwsh", "Perfil do PowerShell", "$PROFILE", "Detectado", true),
            ("vscode", "VS Code", "settings, atalhos, snippets e extensões", "Detectado", true),
            ("git", "Git", @"%USERPROFILE%\.gitconfig", "Detectado", true),
            ("obs", "OBS Studio", @"%APPDATA%\obs-studio", "Detectado", true),
            ("sbot", "Streamer.bot", "pasta data do Streamer.bot", "Detectado", true),
            ("komorebi", "Komorebi", "komorebi.json, applications.yaml", "Detectado", true),
            ("yasb", "YASB", @"%USERPROFILE%\.config\yasb", "Detectado", false));
        WinRows.ItemsSource = _win;
        AppRows.ItemsSource = _apps;
        AppCount.Text = $"{_apps.Count} detectados";
        Count();
    }

    List<CheckItem> Items(params (string Key, string Name, string Desc, string Meta, bool On)[] defs) => defs.Select(d =>
    {
        var i = new CheckItem { Key = d.Key, Title = d.Name, Subtitle = d.Desc, Meta = d.Meta, IsChecked = d.On };
        i.Changed += _ => Count();
        return i;
    }).ToList();

    void Count()
    {
        if (_win == null) return;
        WinCount.Text = $"{_win.Count(i => i.IsChecked)} de {_win.Count}";
    }

    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");
    void OnOptions(object sender, RoutedEventArgs e) => App.Shell.Navigate("options");
    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Configurações");
}
