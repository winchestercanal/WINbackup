using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

public sealed partial class DriversPage : Page
{
    List<CheckItem> _all = new();
    readonly ObservableCollection<CheckItem> _rows = new();

    public DriversPage()
    {
        InitializeComponent();
        Rows.ItemsSource = _rows;
        AdminWarn.Visibility = Sys.IsAdmin ? Visibility.Collapsed : Visibility.Visible;
        TabBackup.Checked += (_, _) => SetTab(backup: true);
        TabRestore.Checked += (_, _) => SetTab(backup: false);
        FilterThird.Checked += (_, _) => Filter();
        FilterAll.Checked += (_, _) => Filter();

        // Backups novos têm a pasta Drivers; os primeiros testes geravam Drivers_Backup.zip
        var last = Destinations.Versions()
            .Select(v => Directory.Exists(Path.Combine(v, "Drivers")) ? Path.Combine(v, "Drivers")
                       : File.Exists(Path.Combine(v, "Drivers_Backup.zip")) ? Path.Combine(v, "Drivers_Backup.zip") : null)
            .FirstOrDefault(p => p != null);
        SourceBox.Text = last ?? "";
        SourceBox.PlaceholderText = @"Ex.: E:\Backup\WINbackup_[DATA]\Drivers";
        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        List<DriverInfo> drivers;
        try
        {
            drivers = await DriversModule.ListAsync();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            Loading.Children.Clear();
            Loading.Children.Add(new TextBlock { Text = L.T("Não foi possível ler os drivers: ") + ex.Message, Foreground = UI.ErrSoft, TextWrapping = TextWrapping.Wrap });
            return;
        }

        _all = drivers.Select(d =>
        {
            var item = new CheckItem
            {
                Key = d.Inf + "|" + d.Device,
                Title = d.Device,
                Subtitle = d.ThirdParty ? (d.DeviceCount > 1 ? L.F("{0} · +{1} dispositivos", d.Inf, d.DeviceCount - 1) : d.Inf) : "",
                Col1 = string.IsNullOrEmpty(d.Provider) ? "—" : d.Provider,
                Col2 = DriversModule.FriendlyClass(d.Class),
                Col3 = d.Version,
                Meta = !d.ThirdParty ? L.T("Sim, já vem no Windows") : d.LikelyOnWindowsUpdate ? L.T("Sim, encontra sozinho") : L.T("Não garantido: guarde este"),
                MetaBrush = d.LikelyOnWindowsUpdate ? UI.Muted : UI.Warn,
                Enabled = d.ThirdParty,
                IsChecked = d.ThirdParty,
                Flag = d.LikelyOnWindowsUpdate,
                Data = d,
            };
            item.Changed += _ => UpdateCounts();
            return item;
        }).ToList();
        Loading.Visibility = Visibility.Collapsed;
        Filter();
    }

    void Filter()
    {
        _rows.Clear();
        foreach (var i in _all.Where(i => FilterAll.IsChecked == true || i.Enabled)) _rows.Add(i);
        UpdateCounts();
    }

    IEnumerable<CheckItem> Group(object pill) => _all.Where(i => i.Enabled && (pill == QuickAll || (pill == QuickIn ? i.Flag : !i.Flag)));

    void UpdateCounts()
    {
        var n = _all.Count(i => i.Enabled && i.IsChecked);
        var third = _all.Count(i => i.Enabled);
        SelText.Text = L.F("{0} de {1} drivers de terceiros selecionados", n, third);
        ExportButton.Content = n == 1 ? L.T("Exportar 1 driver") : L.F("Exportar {0} drivers", n);
        ExportButton.IsEnabled = n > 0 && Sys.IsAdmin;
        foreach (var pill in new[] { QuickOut, QuickIn, QuickAll })
        {
            var g = Group(pill).ToList();
            pill.IsChecked = g.Count > 0 && g.All(i => i.IsChecked);
            pill.IsEnabled = g.Count > 0;
        }
    }

    /// <summary>1º clique marca o grupo; 2º clique desmarca só aquele grupo.</summary>
    void OnQuick(object sender, RoutedEventArgs e)
    {
        var g = Group(sender).ToList();
        var allOn = g.All(i => i.IsChecked);
        foreach (var i in g) i.IsChecked = !allOn;
        UpdateCounts();
    }

    void SetTab(bool backup)
    {
        BackupPanel.Visibility = backup ? Visibility.Visible : Visibility.Collapsed;
        FilterHost.Visibility = backup ? Visibility.Visible : Visibility.Collapsed;
        RestorePanel.Visibility = backup ? Visibility.Collapsed : Visibility.Visible;
    }

    async void OnExport(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Settings.Data.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Escolha o destino"), L.T("Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta."));
            return;
        }
        var chosen = _all.Where(i => i.Enabled && i.IsChecked).Select(i => ((DriverInfo)i.Data!).Inf).Distinct().ToList();
        var allSelected = chosen.Count == _all.Where(i => i.Enabled).Select(i => ((DriverInfo)i.Data!).Inf).Distinct().Count();
        App.Shell.Run(new JobSpec { Title = "Drivers", Drivers = true, DriverInfs = allSelected ? null : chosen });
    }

    void OnPickZip(object sender, RoutedEventArgs e)
    {
        var f = FileDialog.PickFile(L.T("Backup de drivers"), L.T("Backup de drivers (*.zip)"), "*.zip");
        if (f != null) SourceBox.Text = f;
    }

    void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var f = FileDialog.PickFolder(L.T("Pasta com os drivers (.inf)"));
        if (f != null) SourceBox.Text = f;
    }

    async void OnInstall(object sender, RoutedEventArgs e)
    {
        var source = SourceBox.Text.Trim().Trim('"');
        if (!(File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) && !Directory.Exists(source))
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Backup não encontrado"), L.T("Escolha a pasta Drivers do backup (ou outra pasta com arquivos .inf, ou um .zip)."));
            return;
        }
        if (!Sys.IsAdmin)
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Precisa de administrador"), L.T("Feche o WINbackup e abra de novo como administrador para instalar drivers."));
            return;
        }
        var ok = await Dialogs.ConfirmAsync(XamlRoot, L.T("Instalar os drivers agora?"),
            L.F("Todos os .inf de {0} serão instalados com o PnPUtil. Pode ser preciso reiniciar no fim.", Path.GetFileName(source.TrimEnd('\\'))),
            L.T("Instalar drivers"), L.T("Agora não"));
        if (ok) App.Shell.Run(new JobSpec { Kind = JobKind.RestoreDrivers, Title = "Restaurar drivers", DriverSource = source });
    }
}
