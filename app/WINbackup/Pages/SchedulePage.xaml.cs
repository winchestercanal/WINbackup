using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Agendamentos (Agendador de Tarefas do Windows) e presets.</summary>
public sealed partial class SchedulePage : Page
{
    static (DayOfWeek Day, string Label, string Full)[] Week =>
    [
        (DayOfWeek.Sunday, "D", L.T("Domingo")), (DayOfWeek.Monday, "S", L.T("Segunda")), (DayOfWeek.Tuesday, "T", L.T("Terça")), (DayOfWeek.Wednesday, "Q", L.T("Quarta")),
        (DayOfWeek.Thursday, "Q", L.T("Quinta")), (DayOfWeek.Friday, "S", L.T("Sexta")), (DayOfWeek.Saturday, "S", L.T("Sábado")),
    ];

    readonly List<(ToggleButton Button, DayOfWeek Day)> _days = new();
    readonly List<(RadioButton Button, string Slug)> _presets = new();
    string _trigger = "time";

    SettingsData S => Settings.Data;
    static FontFamily Mono => (FontFamily)Application.Current.Resources["Mono"];

    public SchedulePage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 1000);
        AdminWarn.Visibility = Sys.IsAdmin ? Visibility.Collapsed : Visibility.Visible;

        foreach (var p in Presets.All)
        {
            var g = new Grid { ColumnSpacing = 14, Padding = new Thickness(16, 12, 16, 12), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = new StackPanel { Spacing = 2 };
            t.Children.Add(new TextBlock { Text = p.Label, FontWeight = FontWeights.SemiBold });
            t.Children.Add(new TextBlock { Text = p.Info, FontSize = 12, Foreground = UI.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
            g.Children.Add(t);
            var slug = new TextBlock { Text = "--preset " + p.Slug, FontFamily = Mono, FontSize = 11.5, Foreground = UI.NeonSoft, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(slug, 1);
            g.Children.Add(slug);
            var view = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Height = 30, Padding = new Thickness(10, 0, 10, 0),
                FontSize = 12, FontWeight = FontWeights.Normal, CornerRadius = new CornerRadius(7), Content = L.T("O que inclui") };
            var index = Array.IndexOf(Presets.All, p);
            view.Click += async (_, _) => await ShowPreset(index);
            Grid.SetColumn(view, 2);
            g.Children.Add(view);
            PresetsPanel.Children.Add(g);
        }

        foreach (var (day, label, full) in Week)
        {
            var d = new ToggleButton { Style = (Style)Application.Current.Resources["DayToggle"], Content = L.Culture.DateTimeFormat.ShortestDayNames[(int)day].ToUpper(L.Culture), IsChecked = day == DayOfWeek.Friday };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(d, full);
            DaysPanel.Children.Add(d);
            _days.Add((d, day));
        }

        for (var i = 0; i < Presets.All.Length; i++)
        {
            var rb = new RadioButton { Style = (Style)Application.Current.Resources["FilterRadio"], Height = 30, Padding = new Thickness(12, 0, 12, 0),
                GroupName = "spreset", Content = Presets.All[i].Label, IsChecked = i == 1 };
            PresetPick.Children.Add(rb);
            _presets.Add((rb, Presets.All[i].Slug));
        }

        // Discos para o gatilho "ao conectar" (pelo ID do volume; rede não tem)
        foreach (var d in Destinations.Candidates().Where(d => !d.Path.StartsWith(@"\\") && Scheduler.VolumeId(d.Path) != null))
            DriveCombo.Items.Add(new ComboBoxItem { Content = $"{d.Label} · {Path.GetPathRoot(d.Path)}", Tag = d.Path });
        var current = Path.GetPathRoot(S.Destination) ?? "";
        DriveCombo.SelectedItem = DriveCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => string.Equals(Path.GetPathRoot((string)i.Tag), current, StringComparison.OrdinalIgnoreCase))
                                  ?? DriveCombo.Items.Cast<ComboBoxItem>().FirstOrDefault();

        OptIncr.IsOn = true;
        OptAc.IsOn = true;
        LoadTasks();
    }

    // ---------- Lista ----------

    void LoadTasks()
    {
        while (TasksPanel.Children.Count > 1) TasksPanel.Children.RemoveAt(1);
        if (S.Schedules.Count == 0)
        {
            TasksPanel.Children.Add(new TextBlock { Text = L.T("Nenhum agendamento ainda. Crie o primeiro ao lado."), Foreground = UI.Muted, FontSize = 13 });
            return;
        }
        foreach (var def in S.Schedules.ToList())
        {
            var state = Scheduler.State(def);
            var preset = Presets.All.FirstOrDefault(p => p.Slug == def.Preset)?.Label ?? def.Preset;
            var g = new Grid { ColumnSpacing = 14 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = UI.Brush("#12203d"),
                Child = new Icon { Data = def.Kind == "drive" ? Icons.Drive : Icons.Clock, Foreground = UI.BlueSoft } });
            var t = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = def.When, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            t.Children.Add(new TextBlock { Text = $"{preset} · {(def.Incremental ? L.T("incremental") : L.T("completo"))}", FontSize = 12, Foreground = UI.Muted });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var info = !state.Exists ? L.T("Tarefa sumiu do Windows")
                : state.Next is { } next ? L.F("Próximo: {0:dd/MM HH:mm}", next)
                : def.LastRun is { } last ? L.F("Último: {0:dd/MM HH:mm}", last) : L.T("Ainda não rodou");
            var n = new TextBlock { Text = info, FontSize = 12, Foreground = state.Exists ? UI.Muted : UI.Warn, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(n, 2);
            g.Children.Add(n);
            var card = new Border { Style = (Style)Application.Current.Resources["Card"], Padding = new Thickness(16, 14, 16, 14),
                BorderBrush = UI.Brush(state.Enabled ? "#22407a" : "#1c2a48"), Child = g };
            var sw = new ToggleButton { Style = (Style)Application.Current.Resources["Switch"], IsChecked = state.Enabled, IsEnabled = state.Exists && Sys.IsAdmin };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, L.T("Ativar ") + def.When);
            sw.Click += async (_, _) =>
            {
                try { Scheduler.SetEnabled(def, sw.IsChecked == true); }
                catch (Exception ex) { await Dialogs.InfoAsync(XamlRoot, L.T("Não foi possível mudar"), ex.Message); }
                LoadTasks();
            };
            Grid.SetColumn(sw, 3);
            g.Children.Add(sw);
            var del = new Button { Style = (Style)Application.Current.Resources["FlatButton"], Width = 32, Height = 32, CornerRadius = new CornerRadius(6),
                Foreground = UI.Muted, Content = new Icon { Data = Icons.Close, Width = 12, Height = 12, StrokeThickness = 2.2 }, IsEnabled = Sys.IsAdmin };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(del, L.T("Excluir ") + def.When);
            del.Click += async (_, _) =>
            {
                if (!await Dialogs.ConfirmAsync(XamlRoot, L.T("Excluir agendamento?"), L.F("{0} · {1}. A tarefa também sai do Agendador de Tarefas do Windows.", def.When, preset), L.T("Excluir"), L.T("Cancelar"), danger: true)) return;
                Scheduler.Delete(def);
                S.Schedules.Remove(def);
                Settings.Save();
                LoadTasks();
            };
            Grid.SetColumn(del, 4);
            g.Children.Add(del);
            TasksPanel.Children.Add(card);
        }
    }

    async Task ShowPreset(int index)
    {
        var spec = Presets.Build(index);
        var parts = new List<string>();
        var groups = spec.Folders.GroupBy(f => FilesModule.GroupTitle(f.Group)).Select(g => L.F("{0} ({1} pastas)", L.T(g.Key), g.Count()));
        parts.AddRange(groups);
        if (spec.Programs) parts.Add(L.T("Programas (winget)"));
        if (spec.WindowsKeys.Count > 0) parts.Add(L.T("Configurações do Windows: ") + string.Join(", ", spec.WindowsKeys.Select(k => L.T(ConfigsModule.WindowsItems.First(w => w.Key == k).Name))));
        if (spec.Drivers) parts.Add(L.T("Drivers"));
        await Dialogs.InfoAsync(XamlRoot, Presets.All[index].Label,
            (parts.Count == 0 ? L.T("Nada selecionado.") : "• " + string.Join("\n• ", parts)) +
            "\n\n" + L.T("O que entra em cada parte segue o que estiver marcado nas telas do menu Backup."));
    }

    // ---------- Novo agendamento ----------

    void OnTrigger(object sender, RoutedEventArgs e)
    {
        if (TimePanel == null) return;
        _trigger = (string)((FrameworkElement)sender).Tag;
        TimePanel.Visibility = _trigger == "time" ? Visibility.Visible : Visibility.Collapsed;
        DrivePanel.Visibility = _trigger == "drive" ? Visibility.Visible : Visibility.Collapsed;
        LogonText.Visibility = _trigger == "logon" ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnDriveChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DriveCombo.SelectedItem is ComboBoxItem { Tag: string path })
            VolumeText.Text = Scheduler.VolumeId(path) ?? "";
    }

    async void OnCreate(object sender, RoutedEventArgs e)
    {
        if (!Sys.IsAdmin)
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Precisa de administrador"), L.T("Abra o WINbackup como administrador para criar agendamentos."));
            return;
        }
        var def = new ScheduleDef
        {
            Kind = _trigger, Preset = _presets.First(p => p.Button.IsChecked == true).Slug,
            Incremental = OptIncr.IsOn, Wake = OptWake.IsOn, AcOnly = OptAc.IsOn,
        };
        if (_trigger == "time")
        {
            def.Days = _days.Where(d => d.Button.IsChecked == true).Select(d => d.Day).ToList();
            if (def.Days.Count == 0) { await Dialogs.InfoAsync(XamlRoot, L.T("Escolha os dias"), L.T("Marque pelo menos um dia da semana.")); return; }
            if (!TimeSpan.TryParseExact(TimeBox.Text.Trim(), [@"h\:mm", @"hh\:mm"], CultureInfo.InvariantCulture, out var time))
            {
                await Dialogs.InfoAsync(XamlRoot, L.T("Horário inválido"), L.T("Use o formato 20:00."));
                return;
            }
            def.Time = time.ToString(@"hh\:mm");
        }
        else if (_trigger == "drive")
        {
            if (DriveCombo.SelectedItem is not ComboBoxItem { Tag: string path } item || Scheduler.VolumeId(path) is not { } volume)
            {
                await Dialogs.InfoAsync(XamlRoot, L.T("Escolha o disco"), L.T("Conecte o HD externo e escolha-o na lista."));
                return;
            }
            def.VolumeId = volume;
            def.VolumeLabel = ((string)item.Content).Split(" · ")[0];
            // Mesma pasta do destino atual, se for neste disco; senão, \Backup
            var root = Path.GetPathRoot(path)!;
            def.VolumeSubPath = S.Destination.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? S.Destination[root.Length..] : L.T("Backup");
        }

        try
        {
            Scheduler.Register(def);
        }
        catch (Exception ex)
        {
            await Dialogs.InfoAsync(XamlRoot, L.T("Não foi possível criar"), ex.Message);
            return;
        }
        S.Schedules.Add(def);
        Settings.Save();
        LoadTasks();
        await Dialogs.InfoAsync(XamlRoot, L.T("Agendamento criado"), $"{def.When} · {Presets.All.First(p => p.Slug == def.Preset).Label}. " +
            L.T("Ele roda pelo Agendador de Tarefas, mesmo com o WINbackup fechado. Se mover a pasta do WINbackup, recrie os agendamentos."));
    }
}
