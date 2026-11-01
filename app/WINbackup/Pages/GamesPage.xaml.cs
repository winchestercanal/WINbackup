using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Controls;
using WINbackup.Models;

namespace WINbackup.Pages;

/// <summary>Prévia do módulo Saves de jogos. Dados de exemplo do protótipo.</summary>
public sealed partial class GamesPage : Page
{
    readonly List<CheckItem> _games;
    bool _bulk;

    public GamesPage()
    {
        InitializeComponent();
        (string Name, string Path, bool Cloud)[] sample =
        [
            ("[Jogo detectado 1]", @"Documentos\My Games\[Jogo]", false),
            ("[Jogo detectado 2]", @"%USERPROFILE%\Saved Games\[Jogo]", false),
            ("[Jogo detectado 3]", @"%LOCALAPPDATA%\[Estúdio]\[Jogo]\Saved", true),
            ("[Jogo detectado 4]", @"%APPDATA%\[Jogo]\saves", false),
            ("[Jogo detectado 5]", @"Steam\userdata\[ID]\[AppID]", true),
            ("[Jogo detectado 6]", @"Documentos\[Estúdio]\[Jogo]", false),
        ];
        _games = sample.Select(g =>
        {
            var i = new CheckItem { Title = g.Name, Subtitle = g.Path, Flag = g.Cloud, Meta = g.Cloud ? "Já na nuvem" : "Só local",
                MetaBrush = g.Cloud ? UI.NeonSoft : UI.Warn, IsChecked = !g.Cloud };
            i.Changed += _ => { if (!_bulk) Count(); };
            return i;
        }).ToList();
        Rows.ItemsSource = _games;
        SkipCloud.IsOn = true;
        SkipCloud.Toggled += (_, on) => { _bulk = true; foreach (var g in _games) g.IsChecked = !(on && g.Flag); _bulk = false; Count(); };
        Count();
    }

    IEnumerable<CheckItem> Group(object pill) => _games.Where(g => pill == QuickAll || (pill == QuickCloud ? g.Flag : !g.Flag));

    void Count()
    {
        var n = _games.Count(g => g.IsChecked);
        SelText.Text = $"{n} de {_games.Count} selecionados";
        SaveButton.Content = $"Salvar {n} saves";
        foreach (var p in new[] { QuickLocal, QuickCloud, QuickAll }) p.IsChecked = Group(p).All(g => g.IsChecked);
    }

    /// <summary>1º clique marca o grupo; 2º clique desmarca só aquele grupo.</summary>
    void OnQuick(object sender, RoutedEventArgs e)
    {
        var g = Group(sender).ToList();
        var allOn = g.All(i => i.IsChecked);
        _bulk = true;
        foreach (var i in g) i.IsChecked = !allOn;
        _bulk = false;
        Count();
    }

    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Saves de jogos");
}
