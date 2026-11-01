using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

public sealed partial class ActivityPage : Page
{
    public ActivityPage()
    {
        InitializeComponent();

        var versions = Destinations.Versions();
        var last = versions.FirstOrDefault();
        var m = last == null ? null : Manifest.Load(last);
        if (last == null)
        {
            (LastText.Text, LastStatus.Text, LastStatus.Foreground, LastDot.Fill) = ("Nenhum ainda", "Faça o primeiro pelo Início", UI.Muted, UI.BoxOff);
        }
        else
        {
            LastText.Text = HomePage.VersionDate(last)?.ToString("dd/MM/yyyy, HH:mm") ?? Path.GetFileName(last);
            var ok = m is { Completed: true } && m.HashMismatches == 0;
            LastStatus.Text = m is not { Completed: true } ? "Incompleto" : m.HashChecked ? (ok ? "Concluído · integridade OK" : "Concluído · arquivos divergentes") : "Concluído";
            LastStatus.Foreground = LastDot.Fill = ok ? UI.Ok : UI.Warn;
        }

        var dest = Destinations.Describe(Settings.Data.Destination);
        DestText.Text = dest.Label;
        var space = Destinations.Space(Settings.Data.Destination);
        DestStatus.Text = space is { } s ? $"Conectado · {Sys.Bytes(s.Free)} livres" : "Indisponível";
        DestStatus.Foreground = DestDot.Fill = space != null ? UI.Ok : UI.Warn;

        // Automações: exemplo do protótipo (agendamentos ainda não existem)
        foreach (var (icon, when, what, status, on) in new[]
                 {
                     (Icons.Clock, "Toda sexta, 20:00", "Só documentos · incremental", "Ativo", true),
                     (Icons.Drive, "Ao conectar HD Externo (E:)", "Backup completo · incremental", "Ativo", true),
                     (Icons.Clock, "Todo dia 1, 09:00", "Ambiente de dev · completo", "Pausado", false),
                 })
        {
            var g = new Grid { ColumnSpacing = 12, Padding = new Thickness(18, 14, 18, 14), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = UI.Brush("#12203d"),
                Child = new Icon { Data = icon, Foreground = UI.BlueSoft } });
            var t = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = when, FontWeight = FontWeights.SemiBold });
            t.Children.Add(new TextBlock { Text = what, FontSize = 12, Foreground = UI.Muted });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var st = new TextBlock { Text = status, FontSize = 12, Foreground = on ? UI.Ok : UI.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(st, 2);
            g.Children.Add(st);
            AutoPanel.Children.Add(g);
        }

        // Atividade recente: as últimas versões reais do destino
        var recent = versions.Take(6).Select(v =>
        {
            var mm = Manifest.Load(v);
            var text = mm == null ? "Backup sem manifesto" : $"{mm.Title}: {(mm.Completed ? "concluído" : "incompleto")}" +
                (mm.HashChecked ? mm.HashMismatches == 0 ? " · SHA-256 OK" : $" · {mm.HashMismatches} divergentes" : "");
            var color = mm is { Completed: true } && mm.HashMismatches == 0 ? UI.Ok : UI.Warn;
            return (text, color, HomePage.VersionDate(v)?.ToString("dd/MM/yyyy HH:mm") ?? "");
        }).ToList();
        if (recent.Count == 0) recent.Add(("Nenhuma atividade ainda", UI.BoxOff, ""));
        foreach (var (text, color, when) in recent)
        {
            var g = new Grid { ColumnSpacing = 12, Padding = new Thickness(18, 12, 18, 12), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 8, Height = 8, Fill = color, VerticalAlignment = VerticalAlignment.Center });
            var t = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var d = new TextBlock { Text = when, FontSize = 12, Foreground = UI.Muted, FontFamily = (FontFamily)Application.Current.Resources["Mono"], VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(d, 2);
            g.Children.Add(d);
            RecentPanel.Children.Add(g);
        }
    }

    void OnSchedule(object sender, RoutedEventArgs e) => App.Shell.Navigate("schedule");
    void OnHistory(object sender, RoutedEventArgs e) => App.Shell.Navigate("history");
}
