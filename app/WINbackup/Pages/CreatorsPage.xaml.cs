using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Backup de configurações, presets e plugins de programas de criadores.</summary>
public sealed partial class CreatorsPage : Page
{
    readonly List<(CheckItem Item, CreatorApps.App App, List<string> Paths)> _rows = new();
    readonly Dictionary<string, long> _sizes = new();
    readonly CancellationTokenSource _measure = new();

    SettingsData S => Settings.Data;

    public CreatorsPage()
    {
        InitializeComponent();
        PluginsToggle.IsOn = S.CreatorPlugins;
        PluginsToggle.Toggled += (_, on) =>
        {
            S.CreatorPlugins = on;
            foreach (var (item, app, _) in _rows.Where(r => r.App.Plugins))
            {
                item.Enabled = on;
                item.IsChecked = on && CreatorApps.IsSelected(app);
            }
            Settings.Save();
            UpdateTotals();
        };

        var template = (DataTemplate)Resources["AppRow"];
        var missing = new List<string>();
        foreach (var category in CreatorApps.Categories)
        {
            var found = new List<CheckItem>();
            foreach (var app in CreatorApps.Catalog.Where(a => a.Category == category))
            {
                var paths = CreatorApps.Resolve(app);
                if (paths.Count == 0) { missing.Add(app.Name); continue; }
                var item = new CheckItem
                {
                    Key = app.Key, Title = app.Name, Subtitle = app.Note, Flag = app.Plugins, Meta = "…",
                    Col1 = paths.Count == 1 ? Sys.Pretty(paths[0]) : $"{Sys.Pretty(paths[0])} +{paths.Count - 1}",
                    Enabled = !app.Plugins || S.CreatorPlugins,
                    IsChecked = CreatorApps.IsSelected(app) && (!app.Plugins || S.CreatorPlugins),
                };
                item.Changed += i =>
                {
                    if (!i.Enabled) return;
                    S.CreatorSelection[app.Key] = i.IsChecked;
                    Settings.Save();
                    UpdateTotals();
                };
                _rows.Add((item, app, paths));
                found.Add(item);
                Measure(item, paths);
            }
            if (found.Count == 0) continue;

            var header = new Grid { Padding = new Thickness(18, 14, 18, 14), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            header.Children.Add(new TextBlock { Text = category, FontSize = 15, FontWeight = FontWeights.SemiBold });
            header.Children.Add(new TextBlock { Text = found.Count == 1 ? "1 encontrado" : $"{found.Count} encontrados", FontSize = 12, Foreground = UI.Muted,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
            var list = new ItemsRepeater { ItemTemplate = template, ItemsSource = found };
            var panel = new StackPanel();
            panel.Children.Add(header);
            panel.Children.Add(list);
            CategoriesPanel.Children.Add(new Border { Style = (Style)Application.Current.Resources["TableCard"], Child = panel });
        }

        if (_rows.Count == 0)
            CategoriesPanel.Children.Add(new TextBlock { Text = "Nenhum programa de criação encontrado neste PC. Use \"Adicionar pasta\" para incluir pastas manualmente.",
                Foreground = UI.Muted, TextWrapping = TextWrapping.Wrap });
        MissingText.Text = missing.Count == 0 ? "" : "Não encontrados neste PC: " + string.Join(", ", missing) + ".";
        FoundText.Text = _rows.Count == 1 ? "1 programa encontrado" : $"{_rows.Count} programas encontrados";
        LoadCustom();
        UpdateTotals();
        Unloaded += (_, _) => _measure.Cancel();
    }

    void Measure(CheckItem item, List<string> paths)
    {
        var token = _measure.Token;
        var excl = S.Exclusions.Concat(CreatorApps.Exclusions).ToList();
        _ = Task.Run(() =>
        {
            try
            {
                var bytes = paths.Sum(p => FilesModule.Measure(p, excl, token).Bytes);
                DispatcherQueue.TryEnqueue(() =>
                {
                    _sizes[item.Key] = bytes;
                    item.Meta = Sys.Bytes(bytes);
                    item.Refresh();
                    UpdateTotals();
                });
            }
            catch (OperationCanceledException) { }
        });
    }

    void UpdateTotals()
    {
        var chosen = _rows.Where(r => r.Item.Enabled && r.Item.IsChecked).ToList();
        var pending = chosen.Any(r => !_sizes.ContainsKey(r.Item.Key));
        SizeText.Text = $"{Sys.Bytes(chosen.Sum(r => _sizes.GetValueOrDefault(r.Item.Key)))}{(pending ? "+" : "")} · {chosen.Count} selecionados";
        StartButton.IsEnabled = chosen.Count > 0 || S.CreatorCustomFolders.Count > 0;
    }

    void LoadCustom()
    {
        CustomPanel.Children.Clear();
        foreach (var path in S.CreatorCustomFolders)
        {
            var g = new Grid { ColumnSpacing = 10 };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock { Text = path, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Directory.Exists(path) ? UI.Text : UI.Warn });
            var remove = new Button { Style = (Style)Application.Current.Resources["SmallButton"], Height = 30, FontSize = 12, Content = "Remover" };
            remove.Click += (_, _) => { S.CreatorCustomFolders.Remove(path); Settings.Save(); LoadCustom(); UpdateTotals(); };
            Grid.SetColumn(remove, 1);
            g.Children.Add(remove);
            CustomPanel.Children.Add(g);
        }
    }

    void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var path = FileDialog.PickFolder("Adicionar pasta ao backup de Criadores");
        if (path == null || S.CreatorCustomFolders.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        S.CreatorCustomFolders.Add(path);
        Settings.Save();
        LoadCustom();
        UpdateTotals();
    }

    async void OnStart(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(S.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, "Escolha o destino", "Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta.");
            return;
        }
        var spec = Presets.Creators();
        if (spec.Folders.Count == 0)
        {
            await Dialogs.InfoAsync(XamlRoot, "Nada selecionado", "Marque pelo menos um programa ou adicione uma pasta.");
            return;
        }
        App.Shell.Run(spec);
    }
}
