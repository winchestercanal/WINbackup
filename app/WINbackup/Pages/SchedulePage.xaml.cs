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

/// <summary>Prévia dos agendamentos. Os presets listados são os reais do Início.</summary>
public sealed partial class SchedulePage : Page
{
    public SchedulePage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 1000);
        var mono = (FontFamily)Application.Current.Resources["Mono"];

        foreach (var (icon, when, what, next) in new[]
                 {
                     (Icons.Clock, "Toda sexta, 20:00", "Só documentos · incremental", "Próximo: [DATA]"),
                     (Icons.Drive, "Ao conectar HD Externo (E:)", "Backup completo · incremental", "Último: [DATA]"),
                     (Icons.Clock, "Todo dia 1, 09:00", "Ambiente de dev · completo", "Próximo: [DATA]"),
                 })
        {
            var g = new Grid { ColumnSpacing = 14 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = UI.Brush("#12203d"), Child = new Icon { Data = icon, Foreground = UI.BlueSoft } });
            var t = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(new TextBlock { Text = when, FontSize = 14, FontWeight = FontWeights.SemiBold });
            t.Children.Add(new TextBlock { Text = what, FontSize = 12, Foreground = UI.Muted });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var n = new TextBlock { Text = next, FontSize = 12, Foreground = UI.Muted, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(n, 2);
            g.Children.Add(n);
            var card = new Border { Style = (Style)Application.Current.Resources["Card"], Padding = new Thickness(16, 14, 16, 14), BorderBrush = UI.Brush("#22407a"), Child = g };
            var sw = new ToggleButton { Style = (Style)Application.Current.Resources["Switch"], IsChecked = true };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, "Ativar " + when);
            sw.Click += (_, _) => card.BorderBrush = UI.Brush(sw.IsChecked == true ? "#22407a" : "#1c2a48");
            Grid.SetColumn(sw, 3);
            g.Children.Add(sw);
            TasksPanel.Children.Add(card);
        }

        foreach (var p in Presets.All)
        {
            var g = new Grid { ColumnSpacing = 14, Padding = new Thickness(16, 12, 16, 12), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = new StackPanel { Spacing = 2 };
            t.Children.Add(new TextBlock { Text = p.Name, FontWeight = FontWeights.SemiBold });
            t.Children.Add(new TextBlock { Text = p.Description, FontSize = 12, Foreground = UI.Muted, TextTrimming = TextTrimming.CharacterEllipsis });
            g.Children.Add(t);
            var slug = new TextBlock { Text = "--preset " + p.Slug, FontFamily = mono, FontSize = 11.5, Foreground = UI.NeonSoft, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(slug, 1);
            g.Children.Add(slug);
            var edit = new Button { Style = (Style)Application.Current.Resources["SecondaryButton"], Height = 30, Padding = new Thickness(10, 0, 10, 0), FontSize = 12, FontWeight = FontWeights.Normal, CornerRadius = new CornerRadius(7), Content = "Editar" };
            edit.Click += OnSoon;
            Grid.SetColumn(edit, 2);
            g.Children.Add(edit);
            PresetsPanel.Children.Add(g);
        }

        foreach (var (label, full, on) in new[] { ("D", "Domingo", false), ("S", "Segunda", false), ("T", "Terça", false), ("Q", "Quarta", false), ("Q", "Quinta", false), ("S", "Sexta", true), ("S", "Sábado", false) })
        {
            var d = new ToggleButton { Style = (Style)Application.Current.Resources["DayToggle"], Content = label, IsChecked = on };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(d, full);
            DaysPanel.Children.Add(d);
        }

        for (var i = 0; i < Presets.All.Length; i++)
            PresetPick.Children.Add(new RadioButton { Style = (Style)Application.Current.Resources["FilterRadio"], Height = 30, Padding = new Thickness(12, 0, 12, 0),
                GroupName = "spreset", Content = Presets.All[i].Name, IsChecked = i == 1 });

        DriveName.Text = Destinations.Describe(Settings.Data.Destination).Label;
        OptIncr.IsOn = true;
        OptAc.IsOn = true;
    }

    void OnTrigger(object sender, RoutedEventArgs e)
    {
        if (TimePanel == null) return;
        var tag = (string)((FrameworkElement)sender).Tag;
        TimePanel.Visibility = tag == "time" ? Visibility.Visible : Visibility.Collapsed;
        DrivePanel.Visibility = tag == "drive" ? Visibility.Visible : Visibility.Collapsed;
        LogonText.Visibility = tag == "logon" ? Visibility.Visible : Visibility.Collapsed;
    }

    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Agendamentos");
}
