using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

public sealed partial class FilesPage : Page
{
    readonly ObservableCollection<CheckItem> _items = new();
    readonly Dictionary<string, long> _sizes = new();
    CancellationTokenSource? _measure;
    bool _loading;

    SettingsData S => Settings.Data;

    public FilesPage()
    {
        InitializeComponent();
        FolderRepeater.ItemsSource = _items;

        _loading = true;
        ModeC.IsChecked = S.FilesMode == "C";
        ModeS.IsChecked = S.FilesMode != "C";
        CloudSwitch.IsChecked = S.DownloadCloudFiles;
        OptVss.IsOn = S.UseShadowCopy;
        OptIncr.IsOn = S.Incremental;
        OptAcl.IsOn = S.KeepAcl;
        _loading = false;

        ModeC.Checked += (_, _) => SetMode("C");
        ModeS.Checked += (_, _) => SetMode("S");
        CloudSwitch.Checked += (_, _) => { S.DownloadCloudFiles = true; Settings.Save(); };
        CloudSwitch.Unchecked += (_, _) => { S.DownloadCloudFiles = false; Settings.Save(); };
        OptVss.Toggled += (_, on) => { S.UseShadowCopy = on; Settings.Save(); };
        OptIncr.Toggled += (_, on) => { S.Incremental = on; Settings.Save(); };
        OptAcl.Toggled += (_, on) => { S.KeepAcl = on; Settings.Save(); };

        LoadFolders();
        LoadExclusions();
        Loaded += (_, _) => Settings.DestinationChanged += UpdateEstimate;
        Unloaded += (_, _) => { Settings.DestinationChanged -= UpdateEstimate; _measure?.Cancel(); };
    }

    void LoadFolders()
    {
        _loading = true;
        _items.Clear();
        var inCloud = new List<string>();
        foreach (var f in PersonalFolders.All())
        {
            var exists = Directory.Exists(f.Path);
            var cloud = Sys.IsInOneDrive(f.Path);
            if (cloud && !f.Custom) inCloud.Add(L.T(f.Name));
            var item = new CheckItem
            {
                Key = f.Key, Title = f.Custom ? f.Name : L.T(f.Name), Subtitle = Sys.Pretty(f.Path), Flag = cloud, Data = f, Enabled = exists,
                Meta = !exists ? L.T("Não existe") : f.Sensitive ? L.T("Sensível") : f.Custom ? L.T("Personalizada") : "…",
                MetaBrush = !exists ? UI.Label : f.Sensitive ? UI.Warn : UI.Muted,
                IsChecked = exists && PersonalFolders.IsSelected(f),
            };
            item.Changed += OnItemChanged;
            _items.Add(item);
        }
        _items.Add(new CheckItem { Key = "__add", IsAddButton = true });
        _loading = false;

        if (inCloud.Count > 0)
        {
            CloudBanner.Visibility = Visibility.Visible;
            CloudTitle.Text = inCloud.Count == 1 ? L.F("{0} está no OneDrive", inCloud[0]) : L.F("{0} estão no OneDrive", JoinPt(inCloud));
        }
        UpdateCount();
        Measure();
    }

    static string JoinPt(List<string> list) => list.Count == 1 ? list[0] : string.Join(", ", list.Take(list.Count - 1)) + L.T(" e ") + list[^1];

    void OnItemChanged(CheckItem item)
    {
        if (_loading) return;
        // Mexer numa pasta no modo Completo passa para o Seletivo, como no protótipo
        S.FilesMode = "S";
        foreach (var i in _items.Where(i => !i.IsAddButton && i.Enabled)) S.FolderSelection[i.Key] = i.IsChecked;
        Settings.Save();
        if (ModeS.IsChecked != true)
        {
            _loading = true;
            ModeS.IsChecked = true;
            _loading = false;
        }
        UpdateCount();
    }

    void SetMode(string mode)
    {
        if (_loading) return;
        S.FilesMode = mode;
        Settings.Save();
        _loading = true;
        foreach (var i in _items.Where(i => !i.IsAddButton && i.Enabled))
            i.IsChecked = PersonalFolders.IsSelected((FolderSpec)i.Data!);
        _loading = false;
        UpdateCount();
    }

    void UpdateCount()
    {
        var n = _items.Count(i => !i.IsAddButton && i.IsChecked);
        SelCount.Text = n == 1 ? L.T("1 pasta selecionada") : L.F("{0} pastas selecionadas", n);
        UpdateEstimate();
    }

    // ---------- Estimativa ----------

    void Measure()
    {
        _measure?.Cancel();
        var cts = _measure = new CancellationTokenSource();
        var folders = _items.Where(i => !i.IsAddButton && i.Enabled).Select(i => (Item: i, Spec: (FolderSpec)i.Data!)).ToList();
        var excl = S.Exclusions.ToList();
        _sizes.Clear();
        EstText.Text = L.T("Calculando…");
        foreach (var (item, spec) in folders)
        {
            if (!spec.Sensitive && !spec.Custom) { item.Meta = "…"; item.Refresh(); }
            _ = Task.Run(() =>
            {
                try
                {
                    var (_, bytes) = FilesModule.Measure(spec.Path, excl, cts.Token);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (cts.IsCancellationRequested) return;
                        _sizes[item.Key] = bytes;
                        if (!spec.Sensitive && !spec.Custom) item.Meta = Sys.Bytes(bytes);
                        else if (spec.Custom) item.Meta = L.F("Personalizada · {0}", Sys.Bytes(bytes));
                        item.Refresh();
                        UpdateEstimate();
                    });
                }
                catch (OperationCanceledException) { }
            });
        }
    }

    void UpdateEstimate()
    {
        var selected = _items.Where(i => !i.IsAddButton && i.Enabled && i.IsChecked).ToList();
        var pending = selected.Any(i => !_sizes.ContainsKey(i.Key));
        var total = selected.Sum(i => _sizes.GetValueOrDefault(i.Key));
        var space = Destinations.Space(S.Destination);
        EstText.Text = space is { } sp ? L.F("{0}{1} de {2} livres", Sys.Bytes(total), (pending ? "+" : ""), Sys.Bytes(sp.Free)) : $"{Sys.Bytes(total)}{(pending ? "+" : "")}";

        if (space is { Free: > 0 } s)
        {
            var frac = Math.Min(1, (double)total / s.Free);
            EstCol.Width = new GridLength(frac, GridUnitType.Star);
            RestCol.Width = new GridLength(1 - frac, GridUnitType.Star);
            (FitText.Text, FitText.Foreground, EstBar.Background) =
                pending ? (L.T("Calculando o tamanho das pastas…"), (Brush)UI.Muted, (Brush)UI.Neon)
                : total < s.Free * 0.8 ? (L.T("Cabe no destino com folga"), UI.Ok, UI.Neon)
                : total < s.Free ? (L.T("Cabe, mas fica apertado"), UI.Warn, UI.Warn)
                : (L.T("Não cabe no destino: escolha outro ou menos pastas"), UI.ErrSoft, UI.ErrSoft);
            if (!pending && S.Incremental && Destinations.Versions().Count > 0 && total >= s.Free * 0.8)
                FitText.Text += L.T(" (no incremental copia só o que mudou)");
        }
        else
        {
            EstCol.Width = new GridLength(0, GridUnitType.Star);
            FitText.Text = L.T("Escolha o destino no menu lateral");
            FitText.Foreground = UI.Warn;
        }
    }

    // ---------- Pastas personalizadas ----------

    void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var path = FileDialog.PickFolder(L.T("Adicionar pasta ao backup"));
        if (path == null) return;
        if (S.CustomFolders.Contains(path, StringComparer.OrdinalIgnoreCase) || PersonalFolders.All().Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
            return;
        S.CustomFolders.Add(path);
        S.FolderSelection["custom:" + path.ToLowerInvariant()] = true;
        Settings.Save();
        LoadFolders();
    }

    void OnFolderContext(UIElement sender, ContextRequestedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not CheckItem { Data: FolderSpec { Custom: true } spec }) return;
        var menu = new MenuFlyout();
        var remove = new MenuFlyoutItem { Text = L.T("Remover pasta da lista") };
        remove.Click += (_, _) =>
        {
            S.CustomFolders.RemoveAll(p => string.Equals(p, spec.Path, StringComparison.OrdinalIgnoreCase));
            S.FolderSelection.Remove(spec.Key);
            Settings.Save();
            LoadFolders();
        };
        menu.Items.Add(remove);
        menu.ShowAt(sender, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions());
        args.Handled = true;
    }

    // ---------- Exclusões ----------

    void LoadExclusions()
    {
        ExclChips.Children.Clear();
        foreach (var x in S.Exclusions)
        {
            var label = x;
            var remove = new Button
            {
                Style = (Style)Application.Current.Resources["FlatButton"], Width = 20, Height = 20, CornerRadius = new CornerRadius(4),
                Foreground = UI.Brush("#9fc3ff"), Content = new Icon { Data = Icons.Close, Width = 10, Height = 10, StrokeThickness = 2.5 },
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, L.T("Remover ") + label);
            remove.Click += (_, _) => { S.Exclusions.Remove(label); Settings.Save(); LoadExclusions(); Measure(); };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, FontFamily = (FontFamily)Application.Current.Resources["Mono"], VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(remove);
            ExclChips.Children.Add(new Border
            {
                Height = 28, Padding = new Thickness(10, 0, 4, 0), CornerRadius = new CornerRadius(6),
                Background = UI.Brush("#12203d"), BorderBrush = UI.Brush("#22407a"), BorderThickness = new Thickness(1), Child = row,
            });
        }
    }

    void OnAddExcl(object sender, RoutedEventArgs e)
    {
        var x = ExclInput.Text.Trim();
        if (x.Length == 0 || S.Exclusions.Contains(x, StringComparer.OrdinalIgnoreCase)) return;
        S.Exclusions.Add(x);
        Settings.Save();
        ExclInput.Text = "";
        LoadExclusions();
        Measure();
    }

    void OnExclKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) OnAddExcl(sender, e);
    }

    // ---------- Ações ----------

    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");

    async void OnStart(object sender, RoutedEventArgs e)
    {
        var folders = _items.Where(i => !i.IsAddButton && i.Enabled && i.IsChecked).Select(i => (FolderSpec)i.Data!).ToList();
        if (string.IsNullOrEmpty(S.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Escolha o destino"), L.T("Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta."));
            return;
        }
        if (folders.Count == 0)
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Nenhuma pasta selecionada"), L.T("Marque pelo menos uma pasta para fazer o backup."));
            return;
        }
        App.Shell.Run(new JobSpec { Title = "Arquivos pessoais", Folders = folders });
    }
}
