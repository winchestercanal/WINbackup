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
            content.Children.Add(new TextBlock { Text = p.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = p.Description, FontSize = 11.5, LineHeight = 16, Foreground = UI.Muted,
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
        BackupSub.Text = $"Preset: {Presets.All[Settings.Data.Preset].Name}";
        var last = Destinations.Versions().FirstOrDefault();
        var root = Path.GetPathRoot(Settings.Data.Destination) ?? "";
        RestoreSub.Text = last == null
            ? "Nenhum backup no destino ainda"
            : $"Último backup: {VersionDate(last):dd/MM/yyyy HH:mm} · {root}";
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
        summary.Children.Add(Row("Preset", preset.Name, mono: false));
        summary.Children.Add(Row("Destino", string.IsNullOrEmpty(spec.Destination) ? "Não escolhido" : spec.Destination, mono: true));
        var runs = new List<string>();
        var personal = spec.Folders.Count(f => f.Group == "Arquivos");
        if (personal > 0) runs.Add($"Arquivos pessoais ({personal} pastas)");
        if (spec.Browsers) runs.Add("Navegadores");
        if (spec.Programs) runs.Add("Programas");
        if (spec.Drivers) runs.Add("Drivers");
        summary.Children.Add(Row("Nesta versão", runs.Count > 0 ? string.Join(", ", runs) : "Nada ainda", mono: false));

        var body = new StackPanel { Spacing = 18 };
        body.Children.Add(summary);
        if (spec.Unavailable.Count > 0)
            body.Children.Add(Dialogs.Para($"Em breve: {string.Join(", ", spec.Unavailable)}. Essas etapas aparecem no progresso, mas ainda não salvam nada.", muted: true));
        body.Children.Add(Dialogs.Para("Você pode continuar usando o PC enquanto o backup roda.", muted: true));

        if (string.IsNullOrEmpty(spec.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, "Escolha o destino", "Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta.");
            return;
        }
        if (!spec.HasWork)
        {
            await Dialogs.InfoAsync(XamlRoot, $"{preset.Name}: em breve",
                $"Este preset usa módulos que ainda não executam nesta versão ({string.Join(", ", spec.Unavailable)}). Escolha Backup completo ou Só documentos.");
            return;
        }
        if (await Dialogs.ConfirmAsync(XamlRoot, "Iniciar o backup agora?", body, "Sim, iniciar backup", "Agora não"))
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
