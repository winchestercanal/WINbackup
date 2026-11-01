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

        // Automações: os agendamentos reais e o estado de cada tarefa no Windows
        var schedules = Settings.Data.Schedules.Select(d => (Def: d, State: Scheduler.State(d))).ToList();
        foreach (var (def, state) in schedules)
        {
            var preset = Presets.All.FirstOrDefault(p => p.Slug == def.Preset)?.Name ?? def.Preset;
            var g = new Grid { ColumnSpacing = 12, Padding = new Thickness(18, 14, 18, 14), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = UI.Brush("#12203d"),
                Child = new Icon { Data = def.Kind == "drive" ? Icons.Drive : Icons.Clock, Foreground = UI.BlueSoft } });
            var t = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = def.When, FontWeight = FontWeights.SemiBold });
            t.Children.Add(new TextBlock { Text = $"{preset} · {(def.Incremental ? "incremental" : "completo")}", FontSize = 12, Foreground = UI.Muted });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var st = new TextBlock { Text = !state.Exists ? "Sumiu do Windows" : state.Enabled ? "Ativo" : "Pausado", FontSize = 12,
                Foreground = !state.Exists ? UI.Warn : state.Enabled ? UI.Ok : UI.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(st, 2);
            g.Children.Add(st);
            AutoPanel.Children.Add(g);
        }
        if (schedules.Count == 0)
            AutoPanel.Children.Add(new TextBlock { Text = "Nenhum agendamento. Crie em Agendamentos.", Foreground = UI.Muted, Margin = new Thickness(18, 14, 18, 14) });

        var next = schedules.Where(s => s.State.Enabled && s.State.Next != null).OrderBy(s => s.State.Next).FirstOrDefault();
        var onDrive = schedules.FirstOrDefault(s => s.State.Enabled && s.Def.Kind != "time");
        (NextText.Text, NextWhat.Text) = next.Def != null
            ? (next.State.Next!.Value.ToString("ddd, dd/MM HH:mm"), $"{Presets.All.FirstOrDefault(p => p.Slug == next.Def.Preset)?.Name} · {(next.Def.Incremental ? "incremental" : "completo")}")
            : onDrive.Def != null ? (onDrive.Def.When, "quando acontecer") : ("Nenhum", "Crie em Agendamentos");

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
