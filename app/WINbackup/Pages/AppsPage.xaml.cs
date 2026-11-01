using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Controls;
using WINbackup.Models;

namespace WINbackup.Pages;

/// <summary>Prévia do módulo Programas (winget). Dados de exemplo do protótipo.</summary>
public sealed partial class AppsPage : Page
{
    readonly List<CheckItem> _all;
    readonly ObservableCollection<CheckItem> _rows = new();
    string _filter = "all";

    public AppsPage()
    {
        InitializeComponent();
        (string Name, string Id, string Src)[] sample =
        [
            ("7-Zip", "7zip.7zip", "winget"), ("Visual Studio Code", "Microsoft.VisualStudioCode", "winget"),
            ("OBS Studio", "OBSProject.OBSStudio", "winget"), ("Discord", "Discord.Discord", "winget"),
            ("Steam", "Valve.Steam", "winget"), ("VLC", "VideoLAN.VLC", "winget"),
            ("WhatsApp", "9NKSQGP7F2NH", "msstore"), ("[Utilitário do fabricante]", "—", "none"),
        ];
        _all = sample.Select(a =>
        {
            var (label, bg, fg) = a.Src switch
            {
                "winget" => ("winget", "#0b2a2e", "#7befff"),
                "msstore" => ("Microsoft Store", "#12203d", "#9fc3ff"),
                _ => ("Sem pacote", "#2a1f0b", "#ffd89a"),
            };
            var item = new CheckItem { Key = a.Id, Title = a.Name, Col1 = a.Id, Col2 = "[x.x]", Col3 = a.Src, Meta = label,
                MetaBrush = UI.Brush(fg), Fill = UI.Brush(bg), Enabled = a.Src != "none", IsChecked = a.Src != "none" };
            item.Changed += _ => Count();
            return item;
        }).ToList();
        Rows.ItemsSource = _rows;
        StatAll.Text = _all.Count.ToString();
        StatAuto.Text = _all.Count(i => i.Enabled).ToString();
        StatManual.Text = _all.Count(i => !i.Enabled).ToString();
        Apply();
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

    void Count() => SaveButton.Content = $"Salvar {_all.Count(i => i.IsChecked)} programas";

    void OnFilter(object sender, RoutedEventArgs e)
    {
        _filter = (string)((FrameworkElement)sender).Tag;
        if (_all != null) Apply();
    }

    void OnSearch(object sender, TextChangedEventArgs e) { if (_all != null) Apply(); }

    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Programas");
}
