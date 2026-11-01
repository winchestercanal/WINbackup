using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Saves de jogos detectados nas pastas mais comuns.</summary>
public sealed partial class GamesPage : Page
{
    List<CheckItem> _games = new();
    List<GameSave> _found = new();
    readonly ObservableCollection<CheckItem> _rows = new();
    readonly CancellationTokenSource _cts = new();
    bool _bulk;

    SettingsData S => Settings.Data;

    public GamesPage()
    {
        InitializeComponent();
        Rows.ItemsSource = _rows;
        Unloaded += (_, _) => _cts.Cancel();
        SkipCloud.IsOn = S.SkipCloudGames;
        SkipCloud.Toggled += (_, on) =>
        {
            S.SkipCloudGames = on;
            // A escolha de "pular nuvem" vale de novo para todos os jogos na nuvem
            foreach (var g in _found.Where(g => g.Cloud)) S.GamesSelection.Remove(g.Key);
            Settings.Save();
            _bulk = true;
            foreach (var i in _games) i.IsChecked = GamesModule.IsSelected((GameSave)i.Data!);
            _bulk = false;
            Count();
        };

        foreach (var lib in GamesModule.SteamLibraries())
            LibrariesPanel.Children.Add(new TextBlock { Text = lib, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], FontSize = 12.5, Foreground = UI.Brush("#d5e1f7") });
        if (LibrariesPanel.Children.Count == 0)
            LibrariesPanel.Children.Add(new TextBlock { Text = "Steam não encontrada.", FontSize = 12.5, Foreground = UI.Muted });

        _ = LoadAsync();
    }

    async Task LoadAsync()
    {
        Loading.Visibility = Visibility.Visible;
        _found = await Task.Run(GamesModule.Detect);
        _games = _found.Select(g =>
        {
            var i = new CheckItem
            {
                Key = g.Key, Title = g.Name, Subtitle = Sys.Pretty(g.Path), Col1 = "…", Col2 = g.Source, Data = g,
                Meta = g.Cloud ? "Já na nuvem" : "Só local", MetaBrush = g.Cloud ? UI.NeonSoft : UI.Warn, Flag = g.Cloud,
                IsChecked = GamesModule.IsSelected(g),
            };
            i.Changed += item =>
            {
                if (_bulk) return;
                S.GamesSelection[item.Key] = item.IsChecked;
                Settings.Save();
                Count();
            };
            return i;
        }).ToList();
        foreach (var i in _games) _rows.Add(i);
        Loading.Visibility = Visibility.Collapsed;

        StatLocal.Text = Jogos(_found.Count(g => g.Source.StartsWith("Documentos") || g.Source == "Saved Games" || g.Source.StartsWith("Adicionado")));
        StatEngine.Text = Jogos(_found.Count(g => g.Source is "Unreal Engine" or "Unity"));
        StatCloud.Text = Jogos(_found.Count(g => g.Cloud));
        Count();
        Measure();
    }

    static string Jogos(int n) => n == 1 ? "1 jogo" : $"{n} jogos";

    void Measure()
    {
        var token = _cts.Token;
        foreach (var item in _games)
        {
            var path = ((GameSave)item.Data!).Path;
            _ = Task.Run(() =>
            {
                try
                {
                    var bytes = FilesModule.Measure(path, S.Exclusions, token).Bytes;
                    DispatcherQueue.TryEnqueue(() => { item.Col1 = Sys.Bytes(bytes); item.Refresh(); });
                }
                catch (OperationCanceledException) { }
            });
        }
    }

    IEnumerable<CheckItem> Group(object pill) => _games.Where(g => pill == QuickAll || (pill == QuickCloud ? g.Flag : !g.Flag));

    void Count()
    {
        var n = _games.Count(g => g.IsChecked);
        SelText.Text = $"{n} de {_games.Count} selecionados";
        SaveButton.Content = n == 1 ? "Salvar 1 save" : $"Salvar {n} saves";
        SaveButton.IsEnabled = n > 0;
        foreach (var p in new[] { QuickLocal, QuickCloud, QuickAll })
        {
            var g = Group(p).ToList();
            p.IsChecked = g.Count > 0 && g.All(x => x.IsChecked);
            p.IsEnabled = g.Count > 0;
        }
    }

    /// <summary>1º clique marca o grupo; 2º clique desmarca só aquele grupo.</summary>
    void OnQuick(object sender, RoutedEventArgs e)
    {
        var g = Group(sender).ToList();
        var allOn = g.All(i => i.IsChecked);
        _bulk = true;
        foreach (var i in g)
        {
            i.IsChecked = !allOn;
            S.GamesSelection[i.Key] = !allOn;
        }
        _bulk = false;
        Settings.Save();
        Count();
    }

    async void OnAddGame(object sender, RoutedEventArgs e)
    {
        var path = FileDialog.PickFolder("Pasta de save do jogo");
        if (path == null || S.GameCustomFolders.Contains(path, StringComparer.OrdinalIgnoreCase)) return;
        S.GameCustomFolders.Add(path);
        Settings.Save();
        _rows.Clear();
        await LoadAsync();
    }

    async void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(S.Destination))
        {
            await Dialogs.InfoAsync(XamlRoot, "Escolha o destino", "Clique em Destino dos backups, no menu lateral, e escolha um HD externo ou uma pasta.");
            return;
        }
        App.Shell.Run(new JobSpec { Title = "Saves de jogos", FilesLabel = "Saves de jogos", Games = true, Folders = GamesModule.SelectedFolders(_found) });
    }
}
