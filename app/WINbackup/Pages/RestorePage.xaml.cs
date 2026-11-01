using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Prévia da restauração seletiva. Dados de exemplo do protótipo, com o fluxo do "Não reiniciar".</summary>
public sealed partial class RestorePage : Page
{
    readonly List<CheckItem> _tree;

    public RestorePage()
    {
        InitializeComponent();
        Responsive.StackWhenNarrow(MainGrid, 900);
        SourceBox.Text = string.IsNullOrEmpty(Settings.Data.Destination) ? @"E:\Backup" : Settings.Data.Destination;

        (string Kind, bool Locked)[] versions = [("Completo", false), ("Incremental", false), ("Incremental", false), ("Completo", true)];
        for (var i = 0; i < versions.Length; i++)
        {
            var v = versions[i];
            var g = new Grid { ColumnSpacing = 10 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var dot = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1.5), VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(dot);
            var t = new StackPanel { Spacing = 2 };
            t.Children.Add(new TextBlock { Text = "[AAAA-MM-DD HH:MM]", FontSize = 13, FontWeight = FontWeights.SemiBold, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"] });
            t.Children.Add(new TextBlock { Text = $"{v.Kind} · [TAMANHO]", FontSize = 12, Foreground = UI.Muted });
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            if (v.Locked)
            {
                var lk = new Icon { Data = Icons.Lock, Width = 14, Height = 14, StrokeThickness = 2, Foreground = UI.NeonSoft, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(lk, 2);
                g.Children.Add(lk);
            }
            var rb = new RadioButton { GroupName = "ver", Content = g, HorizontalAlignment = HorizontalAlignment.Stretch, IsChecked = i == 0, Tag = v.Locked };
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
            rb.Checked += (_, _) => { Paint(); PasswordCard.Visibility = (bool)rb.Tag ? Visibility.Visible : Visibility.Collapsed; };
            rb.Unchecked += (_, _) => Paint();
            Paint();
            VersionsPanel.Children.Add(rb);
        }

        (int Level, bool Folder, string Name, string Meta, bool On)[] tree =
        [
            (0, true, "Arquivos pessoais", "[TAM]", true), (1, true, "Documentos", "[N] itens", true),
            (2, false, "Contrato.pdf", "[TAM]", true), (2, false, "Planilha gastos.xlsx", "[TAM]", true),
            (1, true, "Vídeos", "[N] itens", false), (1, true, "Downloads", "[N] itens", false),
            (0, true, "Programas", "Programas.json", true), (0, true, "Configurações", "[N] itens", true),
            (1, false, "Redes Wi-Fi", "[N] perfis", true), (1, false, "Windows Terminal", "settings.json", true),
            (0, true, "Drivers", "[N] drivers", true), (0, true, "Navegadores", "[N] perfis", true),
        ];
        _tree = tree.Select(n =>
        {
            var i = new CheckItem { Title = n.Name, Meta = n.Meta, Icon = n.Folder ? Icons.Folder : Icons.DocPlain, MetaBrush = n.Folder ? UI.BlueSoft : UI.Muted,
                Weight = n.Level == 0 ? FontWeights.SemiBold : FontWeights.Normal, Indent = new Thickness(16 + n.Level * 24, 8, 16, 8), IsChecked = n.On };
            i.Changed += _ => Count();
            return i;
        }).ToList();
        Tree.ItemsSource = _tree;
        Count();

        NoReboot.Toggled += async (row, on) =>
        {
            if (on && !await Dialogs.NoRebootAsync(XamlRoot)) row.SetQuietly(false);
            var off = !row.IsOn;
            NoRebootStatus.Text = off ? "Reinicia após os drivers" : "Sem reinício · por sua conta";
            NoRebootStatus.Foreground = off ? UI.Muted : UI.Brush("#ff8fa1");
        };
    }

    void Count() => RestoreButton.Content = $"Restaurar {_tree?.Count(i => i.IsChecked) ?? 0} itens";

    void OnTarget(object sender, RoutedEventArgs e)
    {
        if (TargetBox == null) return;
        var tag = (string)((FrameworkElement)sender).Tag;
        TargetBox.Visibility = tag == "me" ? Visibility.Collapsed : Visibility.Visible;
        TargetBox.Text = tag == "user" ? @"C:\Users\[outro usuário]" : @"D:\Restaurado";
    }

    void OnBrowse(object sender, RoutedEventArgs e)
    {
        var f = FileDialog.PickFolder("Pasta do backup");
        if (f != null) SourceBox.Text = f;
    }

    void OnWizard(object sender, RoutedEventArgs e) => App.Shell.Navigate("wizard");
    void OnCancel(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");
    async void OnSoon(object sender, RoutedEventArgs e) => await Dialogs.SoonAsync(XamlRoot, "Restaurar backup");
}
