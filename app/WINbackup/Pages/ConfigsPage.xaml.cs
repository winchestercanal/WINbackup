using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Configurações do Windows e de apps detectados.</summary>
public sealed partial class ConfigsPage : Page
{
    readonly List<CheckItem> _win = new(), _apps = new();
    readonly CancellationTokenSource _cts = new();

    SettingsData S => Settings.Data;

    public ConfigsPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => _cts.Cancel();

        foreach (var w in ConfigsModule.WindowsItems)
            _win.Add(Item(w.Key, L.T(w.Name), L.T(w.Description), w.Key is "theme" or "env" or "hosts" ? "" : "…"));
        WinRows.ItemsSource = _win;

        foreach (var app in ConfigsModule.Apps)
        {
            var paths = ConfigsModule.Resolve(app);
            if (paths.Count == 0) continue;
            _apps.Add(Item(app.Key, L.T(app.Name), L.T(app.Note), L.T("Detectado")));
        }
        AppRows.ItemsSource = _apps;
        AppCount.Text = _apps.Count == 1 ? L.T("1 detectado") : L.F("{0} detectados", _apps.Count);
        LoadCustom();
        Count();
        _ = FillCountsAsync();
    }

    CheckItem Item(string key, string name, string desc, string meta)
    {
        var i = new CheckItem { Key = key, Title = name, Subtitle = desc, Meta = meta, IsChecked = ConfigsModule.IsSelected(key) };
        i.Changed += item =>
        {
            S.ConfigsOff.RemoveAll(k => string.Equals(k, item.Key, StringComparison.OrdinalIgnoreCase));
            if (!item.IsChecked) S.ConfigsOff.Add(item.Key);
            Settings.Save();
            Count();
        };
        return i;
    }

    /// <summary>Quantas redes, impressoras e fontes existem (netsh e WMI demoram um pouco).</summary>
    async Task FillCountsAsync()
    {
        try
        {
            var wifi = await ConfigsModule.WifiProfilesAsync(_cts.Token);
            var printers = await Task.Run(ConfigsModule.Printers);
            var fonts = ConfigsModule.UserFontCount();
            Set("wifi", wifi.Count == 1 ? L.T("1 rede") : L.F("{0} redes", wifi.Count));
            Set("print", printers.Count == 1 ? L.T("1 impressora") : L.F("{0} impressoras", printers.Count));
            Set("fonts", fonts == 1 ? L.T("1 fonte") : L.F("{0} fontes", fonts));
        }
        catch (OperationCanceledException) { }
    }

    void Set(string key, string meta)
    {
        var item = _win.First(i => i.Key == key);
        item.Meta = meta;
        item.Refresh();
    }

    void Count()
    {
        WinCount.Text = L.F("{0} de {1}", _win.Count(i => i.IsChecked), _win.Count);
        var any = _win.Any(i => i.IsChecked) || _apps.Any(i => i.IsChecked) || S.ConfigCustomFolders.Count > 0;
        SaveButton.IsEnabled = any;
    }

    void LoadCustom()
    {
        CustomPanel.Children.Clear();
        foreach (var path in S.ConfigCustomFolders.ToList())
        {
            var g = new Grid { ColumnSpacing = 8 };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = path, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var remove = new Button { Style = (Style)Application.Current.Resources["SmallButton"], Height = 28, FontSize = 12, Content = L.T("Remover") };
            remove.Click += (_, _) => { S.ConfigCustomFolders.Remove(path); Settings.Save(); LoadCustom(); Count(); };
            Grid.SetColumn(remove, 1);
            g.Children.Add(remove);
            CustomPanel.Children.Add(g);
        }
    }

    void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var path = FileDialog.PickFolder(L.T("Pasta de configuração para incluir no backup"));
        if (path == null || S.ConfigCustomFolders.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        S.ConfigCustomFolders.Add(path);
        Settings.Save();
        LoadCustom();
        Count();
    }

    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");

    async void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(S.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Escolha o destino"), L.T("Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta."));
            return;
        }
        App.Shell.Run(new JobSpec
        {
            Title = "Configurações", FilesLabel = "Configurações de apps",
            Folders = ConfigsModule.SelectedAppFolders(), WindowsKeys = Presets.WindowsKeys(), Exclusions = ConfigsModule.Exclusions,
        });
    }
}
