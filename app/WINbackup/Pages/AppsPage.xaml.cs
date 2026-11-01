using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Programas instalados (winget e Microsoft Store), lista para reinstalar e lista manual.</summary>
public sealed partial class AppsPage : Page
{
    List<CheckItem> _all = new();
    readonly ObservableCollection<CheckItem> _rows = new();
    readonly CancellationTokenSource _cts = new();
    string _filter = "all";

    public AppsPage()
    {
        InitializeComponent();
        Rows.ItemsSource = _rows;
        Unloaded += (_, _) => _cts.Cancel();
        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        Loading.Visibility = Visibility.Visible;
        LoadingText.Text = "Perguntando ao winget o que está instalado…";
        ScanButton.IsEnabled = SaveButton.IsEnabled = ExportButton.IsEnabled = false;
        _rows.Clear();
        List<InstalledProgram> programs;
        try
        {
            programs = await ProgramsModule.ListAsync(_cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            LoadingText.Text = ex.Message;
            ScanButton.IsEnabled = true;
            return;
        }

        var off = Settings.Data.ProgramsOff.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _all = programs.Select(p =>
        {
            var (label, bg, fg) = p.Source switch
            {
                "winget" => ("winget", "#0b2a2e", "#7befff"),
                "msstore" => ("Microsoft Store", "#12203d", "#9fc3ff"),
                _ => ("Sem pacote", "#2a1f0b", "#ffd89a"),
            };
            var item = new CheckItem
            {
                Key = p.Id, Title = p.Name, Col1 = p.Id, Col3 = p.Packaged ? p.Source : "none",
                Col2 = p.Available.Length > 0 ? $"{p.Version} → {p.Available}" : p.Version,
                Meta = label, MetaBrush = UI.Brush(fg), Fill = UI.Brush(bg), Data = p,
                Enabled = p.Packaged, IsChecked = p.Packaged && !off.Contains(p.Id),
            };
            item.Changed += OnToggle;
            return item;
        }).ToList();

        StatAll.Text = _all.Count.ToString();
        StatAuto.Text = _all.Count(i => i.Enabled).ToString();
        StatManual.Text = _all.Count(i => !i.Enabled).ToString();
        Loading.Visibility = Visibility.Collapsed;
        ScanButton.IsEnabled = ExportButton.IsEnabled = true;
        Apply();
    }

    void OnToggle(CheckItem item)
    {
        var off = Settings.Data.ProgramsOff;
        off.RemoveAll(id => string.Equals(id, item.Key, StringComparison.OrdinalIgnoreCase));
        if (!item.IsChecked) off.Add(item.Key);
        Settings.Save();
        Count();
    }

    void Apply()
    {
        var q = Search.Text.Trim();
        _rows.Clear();
        foreach (var i in _all.Where(i => (_filter == "all" || i.Col3 == _filter) &&
                                          (q.Length == 0 || i.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || i.Col1.Contains(q, StringComparison.OrdinalIgnoreCase))))
            _rows.Add(i);
        Count();
    }

    void Count()
    {
        var n = _all.Count(i => i.Enabled && i.IsChecked);
        SaveButton.Content = n == 1 ? "Salvar 1 programa" : $"Salvar {n} programas";
        SaveButton.IsEnabled = n > 0;
    }

    HashSet<string> ChosenIds() => _all.Where(i => i.Enabled && i.IsChecked).Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    void OnFilter(object sender, RoutedEventArgs e)
    {
        _filter = (string)((FrameworkElement)sender).Tag;
        if (Rows != null) Apply();
    }

    void OnSearch(object sender, TextChangedEventArgs e) { if (Rows != null) Apply(); }

    void OnScan(object sender, RoutedEventArgs e) => _ = LoadAsync();

    async void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Settings.Data.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, "Escolha o destino", "Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta.");
            return;
        }
        App.Shell.Run(new JobSpec { Title = "Programas", Programs = true });
    }

    async void OnExport(object sender, RoutedEventArgs e)
    {
        var folder = FileDialog.PickFolder("Salvar Programas.json nesta pasta");
        if (folder == null) return;
        var target = Path.Combine(folder, "Programas.json");
        ExportButton.IsEnabled = false;
        try
        {
            var n = await ProgramsModule.ExportAsync(target, ChosenIds(), _cts.Token);
            await Dialogs.InfoAsync(XamlRoot, "Lista exportada", $"{n} programas salvos em {target}. Para instalar em outro PC: Programas → Importar lista e instalar.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Dialogs.InfoAsync(XamlRoot, "Não foi possível exportar", ex.Message);
        }
        finally
        {
            ExportButton.IsEnabled = true;
        }
    }

    async void OnImport(object sender, RoutedEventArgs e)
    {
        var file = FileDialog.PickFile("Lista de programas para instalar", "Lista do winget (*.json)", "*.json");
        if (file == null) return;
        List<(string Id, string Source)> packages;
        try
        {
            packages = ProgramsModule.ReadImportFile(file);
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(XamlRoot, "Arquivo inválido", "Esse arquivo não parece uma lista do winget (Programas.json). " + ex.Message);
            return;
        }
        if (packages.Count == 0)
        {
            await Dialogs.InfoAsync(XamlRoot, "Lista vazia", "Nenhum programa nesse arquivo.");
            return;
        }
        var ok = await Dialogs.ConfirmAsync(XamlRoot, $"Instalar {packages.Count} programas?",
            "Cada programa é baixado e instalado pelo winget, um por vez. Os que já estiverem instalados são pulados. Pode demorar e usar bastante internet.",
            "Instalar", "Agora não");
        if (!ok) return;
        App.Shell.Run(new JobSpec
        {
            Kind = JobKind.Restore, Title = "Instalar programas",
            InstallPackages = packages.Select(p => (p.Id, p.Id, p.Source)).ToList(),
        });
    }
}
