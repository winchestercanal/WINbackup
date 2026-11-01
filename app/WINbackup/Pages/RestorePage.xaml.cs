using System.Collections.ObjectModel;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Restauração seletiva: versão, conteúdo, conflitos e destino.</summary>
public sealed partial class RestorePage : Page
{
    record Node(CheckItem Item, string? GroupKey, ManifestFolder? Folder, bool IsDrivers, bool IsPrograms = false, bool IsWindows = false);

    readonly ObservableCollection<CheckItem> _view = new();
    readonly List<Node> _nodes = new();
    readonly HashSet<string> _pickedFiles = new(StringComparer.OrdinalIgnoreCase);
    string? _version;
    Manifest? _manifest;
    bool _syncing;

    public RestorePage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 900);
        Responsive.StackWhenNarrow(OptionsGrid, 820);
        Tree.ItemsSource = _view;
        SourceBox.Text = Settings.Data.Destination;

        NoReboot.Toggled += async (row, on) =>
        {
            if (on && !await Dialogs.NoRebootAsync(XamlRoot)) row.SetQuietly(false);
            var off = !row.IsOn;
            NoRebootStatus.Text = off ? "Reinicia após os drivers" : "Sem reinício · por sua conta";
            NoRebootStatus.Foreground = off ? UI.Muted : UI.Brush("#ff8fa1");
        };
        LoadVersions();
    }

    // ---------- Versões ----------

    void LoadVersions()
    {
        VersionsPanel.Children.Clear();
        _version = null;
        _manifest = null;
        _nodes.Clear();
        var versions = Destinations.Versions(SourceBox.Text.Trim())
            .Select(v => (Path: v, M: Manifest.Load(v)))
            .Where(v => v.M != null)
            .ToList();
        if (versions.Count == 0)
        {
            VersionsPanel.Children.Add(new TextBlock { Text = "Nenhum backup do WINbackup nesta pasta.", Margin = new Thickness(16, 12, 16, 12),
                Foreground = UI.Muted, TextWrapping = TextWrapping.Wrap });
            ShowTree();
            return;
        }

        for (var i = 0; i < versions.Count; i++)
        {
            var (path, m) = versions[i];
            var g = new Grid { ColumnSpacing = 10 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dot = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1.5), VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(dot);
            var t = new StackPanel { Spacing = 2 };
            t.Children.Add(new TextBlock { Text = HomePage.VersionDate(path)?.ToString("dd/MM/yyyy HH:mm") ?? Path.GetFileName(path), FontSize = 13,
                FontWeight = FontWeights.SemiBold, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"] });
            var size = m!.Files.Sum(f => f.Size);
            t.Children.Add(new TextBlock { Text = $"{m.Title} · {m.Kind}{(m.Completed ? "" : " (incompleto)")} · {Sys.Bytes(size)}", FontSize = 12,
                Foreground = m.Completed ? UI.Muted : UI.Warn, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            if (m.Encrypted)
            {
                var lk = new Icon { Data = Icons.Lock, Width = 14, Height = 14, StrokeThickness = 2, Foreground = UI.NeonSoft, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lk, 2);
                g.Children.Add(lk);
            }
            var rb = new RadioButton { GroupName = "ver", Content = g, HorizontalAlignment = HorizontalAlignment.Stretch };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(rb, $"Versão {Path.GetFileName(path)}");
            rb.Template = (ControlTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='RadioButton'>" +
                "<Border Background='{TemplateBinding Background}' Padding='16,11' BorderBrush='#16223d' BorderThickness='0,0,0,1'>" +
                "<ContentPresenter Content='{TemplateBinding Content}'/></Border></ControlTemplate>");
            void Paint()
            {
                var on = rb.IsChecked == true;
                rb.Background = on ? UI.Brush("#0f1b33") : UI.Transparent;
                dot.BorderBrush = on ? UI.Neon : UI.BoxOff;
                dot.Background = on ? UI.Neon : UI.Transparent;
            }
            rb.Checked += (_, _) => { Paint(); SelectVersion(path, m); };
            rb.Unchecked += (_, _) => Paint();
            Paint();
            VersionsPanel.Children.Add(rb);
            if (i == 0) rb.IsChecked = true;
        }
    }

    void SelectVersion(string path, Manifest m)
    {
        _version = path;
        _manifest = m;
        _pickedFiles.Clear();
        SearchBox.Text = "";
        BuildTree();
        ShowTree();
    }

    // ---------- Conteúdo ----------

    static string GroupTitle(string top) => FilesModule.GroupTitle(top);

    void BuildTree()
    {
        _nodes.Clear();
        var m = _manifest!;
        var folders = m.Folders.Select(f => (F: f, Group: RestoreModule.GroupOf(m, f))).ToList();
        foreach (var top in folders.GroupBy(x => x.Group.Split('\\')[0]))
        {
            var keys = top.Select(t => t.F.Key).ToHashSet();
            var files = m.Files.Where(x => keys.Contains(x.Folder)).ToList();
            var head = Item(GroupTitle(top.Key), "", $"{files.Count:N0} arquivos · {Sys.Bytes(files.Sum(x => x.Size))}", Icons.Folder, 0);
            _nodes.Add(new Node(head, top.Key, null, false));
            foreach (var (f, group) in top)
            {
                var ff = m.Files.Where(x => x.Folder == f.Key).ToList();
                var app = group.Contains('\\') ? group[(group.IndexOf('\\') + 1)..] : null;
                var child = Item(app != null ? $"{app} · {f.Name}" : f.Name, Sys.Pretty(f.Source), $"{ff.Count:N0} · {Sys.Bytes(ff.Sum(x => x.Size))}", Icons.Folder, 1);
                _nodes.Add(new Node(child, top.Key, f, false));
            }
        }
        var packaged = m.Programs.Count(p => p.Source is "winget" or "msstore");
        if (packaged > 0)
        {
            var manual = m.Programs.Count - packaged;
            _nodes.Add(new Node(Item("Programas", "Reinstalados pelo winget, um por vez" + (manual > 0 ? $"; {manual} sem pacote ficam na lista manual" : ""),
                $"{packaged} programas", Icons.Apps, 0), null, null, false, true));
        }
        if (m.WindowsSettings.Count > 0)
        {
            var names = m.WindowsSettings.Select(k => ConfigsModule.WindowsItems.FirstOrDefault(w => w.Key == k)?.Name ?? k);
            _nodes.Add(new Node(Item("Configurações do Windows", string.Join(", ", names), $"{m.WindowsSettings.Count} itens", Icons.Configs, 0),
                null, null, false, false, true));
        }
        if (_version != null && (Directory.Exists(Path.Combine(_version, "Drivers")) || File.Exists(Path.Combine(_version, "Drivers_Backup.zip"))))
        {
            var count = m.Drivers.Count;
            _nodes.Add(new Node(Item("Drivers", "Instalados com PnPUtil antes dos arquivos", count > 0 ? $"{count} drivers" : "pasta Drivers", Icons.Drivers, 0), null, null, true));
        }
        foreach (var n in _nodes) n.Item.Changed += OnNodeChanged;
    }

    static CheckItem Item(string title, string subtitle, string meta, string icon, int level) => new()
    {
        Title = title, Subtitle = subtitle, Flag = subtitle.Length > 0, Meta = meta, Icon = icon,
        MetaBrush = level == 0 ? UI.BlueSoft : UI.Muted, Weight = level == 0 ? FontWeights.SemiBold : FontWeights.Normal,
        Indent = new Thickness(16 + level * 24, 8, 16, 8), IsChecked = true,
    };

    void OnNodeChanged(CheckItem item)
    {
        if (_syncing) return;
        _syncing = true;
        var node = _nodes.First(n => n.Item == item);
        if (node.Folder == null && node.GroupKey != null)
            foreach (var c in _nodes.Where(n => n.GroupKey == node.GroupKey && n.Folder != null)) c.Item.IsChecked = item.IsChecked;
        else if (node.Folder != null)
            _nodes.First(n => n.GroupKey == node.GroupKey && n.Folder == null).Item.IsChecked =
                _nodes.Where(n => n.GroupKey == node.GroupKey && n.Folder != null).All(n => n.Item.IsChecked);
        _syncing = false;
        UpdateSummary();
    }

    void ShowTree()
    {
        _view.Clear();
        foreach (var n in _nodes) _view.Add(n.Item);
        TreeEmpty.Visibility = _nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TreeEmpty.Text = _manifest == null ? "Escolha a pasta onde estão os backups." : "Esta versão não tem arquivos.";
        ContentTitle.Text = _version == null ? "Conteúdo do backup" : $"Conteúdo de {Path.GetFileName(_version)}";
        UpdateSummary();
    }

    /// <summary>Busca: mostra arquivos soltos para restaurar só alguns.</summary>
    void OnSearch(object sender, TextChangedEventArgs e)
    {
        if (_manifest == null) return;
        var q = SearchBox.Text.Trim();
        if (q.Length < 2)
        {
            ShowTree();
            return;
        }
        _view.Clear();
        var folders = _manifest.Folders.GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.First());
        foreach (var f in _manifest.Files.Where(f => f.Path.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(300))
        {
            var key = f.Folder + "|" + f.Path;
            var folderName = folders.TryGetValue(f.Folder, out var mf) ? mf.Name : "";
            var item = Item(Path.GetFileName(f.Path), Path.Combine(folderName, Path.GetDirectoryName(f.Path) ?? ""), Sys.Bytes(f.Size), Icons.DocPlain, 0);
            item.Weight = FontWeights.Normal;
            item.MetaBrush = UI.Muted;
            item.IsChecked = _pickedFiles.Contains(key);
            item.Changed += i =>
            {
                if (i.IsChecked) _pickedFiles.Add(key); else _pickedFiles.Remove(key);
                UpdateSummary();
            };
            _view.Add(item);
        }
        TreeEmpty.Visibility = _view.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TreeEmpty.Text = "Nenhum arquivo com esse nome nesta versão.";
        UpdateSummary();
    }

    // ---------- Resumo e opções ----------

    bool FileMode => _pickedFiles.Count > 0;

    List<ManifestFolder> ChosenFolders() => _nodes.Where(n => n.Folder != null && n.Item.IsChecked).Select(n => n.Folder!).ToList();
    bool DriversChosen => !FileMode && _nodes.Any(n => n.IsDrivers && n.Item.IsChecked);
    bool ProgramsChosen => !FileMode && _nodes.Any(n => n.IsPrograms && n.Item.IsChecked);
    bool WindowsChosen => !FileMode && _nodes.Any(n => n.IsWindows && n.Item.IsChecked);

    void UpdateSummary()
    {
        RebootCard.Visibility = DriversChosen ? Visibility.Visible : Visibility.Collapsed;
        if (_manifest == null)
        {
            RestoreButton.Content = "Restaurar";
            RestoreButton.IsEnabled = false;
            return;
        }
        if (FileMode)
        {
            RestoreButton.Content = _pickedFiles.Count == 1 ? "Restaurar 1 arquivo" : $"Restaurar {_pickedFiles.Count} arquivos";
            RestoreButton.IsEnabled = true;
            return;
        }
        var keys = ChosenFolders().Select(f => f.Key).ToHashSet();
        var files = _manifest.Files.Where(f => keys.Contains(f.Folder)).ToList();
        var parts = new List<string>();
        if (keys.Count > 0) parts.Add($"{files.Count:N0} arquivos ({Sys.Bytes(files.Sum(f => f.Size))})");
        if (ProgramsChosen) parts.Add("programas");
        if (WindowsChosen) parts.Add("configurações do Windows");
        if (DriversChosen) parts.Add("drivers");
        RestoreButton.Content = parts.Count == 0 ? "Nada selecionado" : "Restaurar " + string.Join(" e ", parts);
        RestoreButton.IsEnabled = parts.Count > 0;
    }

    RestoreTarget Target => TargetUser.IsChecked == true ? RestoreTarget.OtherUser : TargetFolder.IsChecked == true ? RestoreTarget.Folder : RestoreTarget.Original;
    ConflictMode Conflict => ConfOver.IsChecked == true ? ConflictMode.Overwrite : ConfBoth.IsChecked == true ? ConflictMode.KeepBoth : ConflictMode.Skip;

    void OnTarget(object sender, RoutedEventArgs e)
    {
        if (TargetRow == null) return;
        TargetRow.Visibility = Target == RestoreTarget.Original ? Visibility.Collapsed : Visibility.Visible;
        (TargetBox.PlaceholderText, TargetHint.Text) = Target switch
        {
            RestoreTarget.OtherUser => (@"C:\Users\nome", "Pasta do outro usuário. Documentos, Imagens etc. vão para as pastas padrão dele; o resto, para o caminho equivalente dentro dela."),
            RestoreTarget.Folder => (@"D:\Restaurado", "Tudo vai para dentro desta pasta, organizado como no backup. Bom para conferir antes de substituir algo."),
            _ => ("", "Cada pasta volta para onde estava. Documentos, Imagens e Área de Trabalho vão para onde essas pastas estão hoje (OneDrive incluído)."),
        };
    }

    void OnBrowseTarget(object sender, RoutedEventArgs e)
    {
        var f = FileDialog.PickFolder(Target == RestoreTarget.OtherUser ? "Pasta do usuário (C:\\Users\\nome)" : "Restaurar para esta pasta");
        if (f != null) TargetBox.Text = f;
    }

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        var f = FileDialog.PickFolder("Pasta onde estão os backups");
        if (f == null) return;
        // Aceita também uma pasta de versão escolhida por engano
        if (Path.GetFileName(f).StartsWith("WINbackup_", StringComparison.OrdinalIgnoreCase)) f = Path.GetDirectoryName(f)!;
        SourceBox.Text = f;
        LoadVersions();
    }

    string _lastSource = "";

    void OnSourceKey(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) OnSourceLostFocus(sender, e);
    }

    void OnSourceLostFocus(object sender, RoutedEventArgs e)
    {
        if (SourceBox.Text.Trim() == _lastSource) return;
        _lastSource = SourceBox.Text.Trim();
        LoadVersions();
    }

    // ---------- Ações ----------

    async void OnRestore(object sender, RoutedEventArgs e)
    {
        if (_manifest == null || _version == null) return;
        var target = Target;
        var targetPath = TargetBox.Text.Trim();
        if (target != RestoreTarget.Original && (targetPath.Length < 3 || !Path.IsPathRooted(targetPath)))
        {
            await Dialogs.InfoAsync(XamlRoot, "Escolha o destino", target == RestoreTarget.OtherUser ? "Informe a pasta do outro usuário, como C:\\Users\\nome." : "Escolha a pasta para onde restaurar.");
            return;
        }

        var source = FileMode
            ? new RestoreSource(_version, _manifest, _pickedFiles.Select(k => k.Split('|')[0]).ToHashSet(), new HashSet<string>(_pickedFiles, StringComparer.OrdinalIgnoreCase), false)
            : new RestoreSource(_version, _manifest, ChosenFolders().Select(f => f.Key).ToHashSet(), null, DriversChosen, Programs: ProgramsChosen, WindowsSettings: WindowsChosen);

        var where = target switch
        {
            RestoreTarget.Folder => $"para {targetPath}",
            RestoreTarget.OtherUser => $"para o usuário em {targetPath}",
            _ => "para os locais originais",
        };
        var rule = Conflict switch
        {
            ConflictMode.Overwrite => "Arquivos que já existirem serão SUBSTITUÍDOS pela versão do backup.",
            ConflictMode.KeepBoth => "Se já existir, a cópia do backup entra com \"(restaurado)\" no nome.",
            _ => "Arquivos que já existirem serão mantidos (pulados).",
        };
        var drivers = source.Drivers ? " Os drivers são instalados primeiro" + (NoReboot.IsOn ? ", sem reiniciar no fim." : " e o app pede para reiniciar no fim.") : "";
        if (!_manifest.Completed) rule += " Atenção: esta versão está incompleta (o backup foi cancelado).";
        if (source.Folders?.Any(k => _manifest.Folders.FirstOrDefault(f => f.Key == k) is { } mf && RestoreModule.GroupOf(_manifest, mf).StartsWith("Navegadores")) == true
            && BrowsersModule.Running(BrowsersModule.Detect()) is { Count: > 0 } open)
            rule += $" Feche {string.Join(", ", open)} antes: o navegador aberto sobrescreve os favoritos ao fechar.";
        var ok = await Dialogs.ConfirmAsync(XamlRoot, "Restaurar agora?",
            $"{RestoreButton.Content} da versão de {HomePage.VersionDate(_version):dd/MM/yyyy HH:mm}, {where}. {rule}{drivers}",
            "Restaurar", "Cancelar", danger: Conflict == ConflictMode.Overwrite);
        if (!ok) return;

        App.Shell.Run(new JobSpec
        {
            Kind = JobKind.Restore, Title = "Restaurar backup", RestoreSources = [source],
            Target = target, TargetPath = target == RestoreTarget.Original ? null : targetPath, Conflict = Conflict, NoReboot = NoReboot.IsOn,
        });
    }

    void OnWizard(object sender, RoutedEventArgs e) => App.Shell.Navigate("wizard");
    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");
}
