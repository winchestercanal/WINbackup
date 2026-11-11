using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        for (var i = 0; i < Presets.All.Length; i++)
        {
            var p = Presets.All[i];
            var index = i;
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(new TextBlock { Text = p.Label, FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = p.Info, FontSize = 11.5, LineHeight = 16, Foreground = UI.Muted,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
            var rb = new RadioButton { Style = (Style)Application.Current.Resources["PresetCard"], GroupName = "preset", Tag = p.Icon,
                Content = content, IsChecked = Settings.Data.Preset == i };
            rb.Checked += (_, _) => { Settings.Data.Preset = index; Settings.Save(); Refresh(); };
            Grid.SetColumn(rb, i);
            PresetGrid.Children.Add(rb);
        }
        Loaded += (_, _) => { Settings.DestinationChanged += Refresh; Refresh(); };
        Unloaded += (_, _) => Settings.DestinationChanged -= Refresh;
    }

    void Refresh()
    {
        BackupSub.Text = L.F("Preset: {0}", Presets.All[Settings.Data.Preset].Label);
        var last = Destinations.Versions().FirstOrDefault();
        var root = Path.GetPathRoot(Settings.Data.Destination) ?? "";
        RestoreSub.Text = last == null
            ? L.T("Nenhum backup no destino ainda")
            : L.F("Último backup: {0:g} · {1}", VersionDate(last), root);
    }

    public static DateTime? VersionDate(string versionPath)
    {
        var name = Path.GetFileName(versionPath).Replace("WINbackup_", "");
        return DateTime.TryParseExact(name, "yyyy-MM-dd_HH-mm-ss", null, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    async void OnBackup(object sender, RoutedEventArgs e)
    {
        if (BackupRunner.Current is { IsFinished: false })
        {
            App.Shell.Navigate("progress");
            return;
        }
        var preset = Presets.All[Settings.Data.Preset];
        var spec = Presets.Build(Settings.Data.Preset);

        var summary = new StackPanel { Spacing = 10, Padding = new Thickness(16, 14, 16, 14), CornerRadius = new CornerRadius(10),
            Background = UI.Brush("#0a1120"), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(1) };
        summary.Children.Add(Row(L.T("Preset"), preset.Label, mono: false));
        summary.Children.Add(Row(L.T("Destino"), string.IsNullOrEmpty(spec.Destination) ? L.T("Não escolhido") : spec.Destination, mono: true));
        var runs = new List<string>();
        var personal = spec.Folders.Count(f => f.Group == "Arquivos");
        if (personal > 0) runs.Add(L.F("Arquivos pessoais ({0} pastas)", personal));
        if (spec.Browsers) runs.Add(L.T("Navegadores"));
        if (spec.Programs) runs.Add(L.T("Programas"));
        if (spec.Drivers) runs.Add(L.T("Drivers"));
        summary.Children.Add(Row(L.T("Nesta versão"), runs.Count > 0 ? string.Join(", ", runs) : L.T("Nada ainda"), mono: false));

        var body = new StackPanel { Spacing = 18 };
        body.Children.Add(summary);
        if (spec.Unavailable.Count > 0)
            body.Children.Add(Dialogs.Para(L.F("Em breve: {0}. Essas etapas aparecem no progresso, mas ainda não salvam nada.", string.Join(", ", spec.Unavailable)), muted: true));
        body.Children.Add(Dialogs.Para(L.T("Você pode continuar usando o PC enquanto o backup roda."), muted: true));

        if (string.IsNullOrEmpty(spec.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Escolha o destino"), L.T("Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta."));
            return;
        }
        if (!spec.HasWork)
        {
            await Dialogs.InfoAsync(XamlRoot, L.F("{0}: em breve", preset.Label),
                L.F("Este preset usa módulos que ainda não executam nesta versão ({0}). Escolha Backup completo ou Só documentos.", string.Join(", ", spec.Unavailable)));
            return;
        }
        if (await Dialogs.ConfirmAsync(XamlRoot, L.T("Iniciar o backup agora?"), body, L.T("Sim, iniciar backup"), L.T("Agora não")))
            App.Shell.Run(spec);
    }

    static Grid Row(string label, string value, bool mono)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = UI.Muted });
        var v = new TextBlock { Text = value, FontSize = mono ? 12.5 : 13, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap };
        if (mono) v.FontFamily = (FontFamily)Application.Current.Resources["Mono"];
        Grid.SetColumn(v, 1);
        g.Children.Add(v);
        return g;
    }

    void OnRestore(object sender, RoutedEventArgs e) => App.Shell.Navigate("wizard");
    void OnManagePresets(object sender, RoutedEventArgs e) => App.Shell.Navigate("schedule");
}
