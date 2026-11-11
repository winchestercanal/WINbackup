using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

public sealed partial class ReportPage : Page
{
    readonly BackupRunner? _r = BackupRunner.Current;

    public ReportPage()
    {
        InitializeComponent();
        if (_r == null || !_r.IsFinished)
        {
            TitleText.Text = L.T("Nenhum relatório ainda");
            SubText.Text = L.T("O relatório aparece aqui ao fim de cada backup.");
            ExportButton.Visibility = FolderButton.Visibility = OpenLogLink.Visibility = AttentionTitle.Visibility = Visibility.Collapsed;
            return;
        }
        Fill(_r);
        Loaded += async (_, _) =>
        {
            await MaybeAskRebootAsync(_r);
            await MaybeRemindDonationAsync(_r);
        };
    }

    /// <summary>Restauração com drivers: reiniciar é o padrão, a menos que o usuário tenha marcado "Não reiniciar".</summary>
    async Task MaybeAskRebootAsync(BackupRunner r)
    {
        if (!r.RebootRequired || r.Spec.NoReboot || r.RebootAsked || r.State != RunState.Completed) return;
        r.RebootAsked = true;
        await Task.Delay(400);
        OnReboot(this, new RoutedEventArgs());
    }

    async Task MaybeRemindDonationAsync(BackupRunner r)
    {
        if (r.CountedForDonation || r.State != RunState.Completed || r.ErrorCount > 0 || r.Spec.Kind != JobKind.Backup) return;
        r.CountedForDonation = true;
        if (!Donation.CountSuccessAndCheckReminder()) return;
        await Task.Delay(600);
        if (await Dialogs.DonationReminderAsync(XamlRoot)) App.Shell.Navigate("donate");
    }

    void Fill(BackupRunner r)
    {
        var restore = r.Spec.IsRestore;
        var what = restore ? L.T("Restauração") : L.T("Backup");
        var issues = r.WarningCount + r.ErrorCount;
        (TitleText.Text, var color, var bg, var icon) = r.State switch
        {
            RunState.Canceled => (L.F("{0} cancelado", what), UI.Warn, "#2a1f0b", Icons.Close),
            RunState.Failed => (L.F("{0} não terminou", what), UI.ErrSoft, "#2a0d15", Icons.Warning),
            _ when r.ErrorCount > 0 => (L.F("{0} concluído com erros", what), UI.Warn, "#2a1f0b", Icons.Warning),
            _ => (L.F("{0} concluído", what), UI.Ok, "#0b2a1f", Icons.Check),
        };
        StatusCircle.BorderBrush = color;
        StatusCircle.Background = UI.Brush(bg);
        StatusIcon.Foreground = color;
        StatusIcon.Data = icon;

        var where = r.Spec.Kind switch
        {
            JobKind.RestoreDrivers => r.Spec.DriverSource,
            JobKind.Restore when r.Spec.InstallPackages != null => L.T("lista importada"),
            JobKind.Restore => string.Join(", ", r.Spec.RestoreSources.Select(s => Path.GetFileName(s.VersionPath)).Distinct()),
            _ => r.VersionPath,
        };
        SubText.Text = $"{L.T(r.Spec.Title)} · {where}" + (issues == 0 ? "" : issues == 1 ? L.T(" · com 1 aviso") : L.F(" · com {0} avisos", issues));

        var onlyDrivers = r.Spec.Kind == JobKind.RestoreDrivers;
        Stat(0, onlyDrivers ? L.T("Pacotes de driver") : restore ? L.T("Itens restaurados") : L.T("Itens salvos"), r.ItemsSaved.ToString("N0"), UI.Text);
        if (restore && r.RebootRequired)
            Stat(1, L.T("Reinício"), L.T("Necessário"), UI.Warn);
        else
            Stat(1, onlyDrivers ? L.T("Reinício") : restore ? L.T("Restaurado") : L.T("Tamanho final"), onlyDrivers ? L.T("Não precisa") : Sys.Bytes(r.StoredBytes), UI.Text);
        Stat(2, L.T("Duração"), Sys.Duration(r.Elapsed), UI.Text);
        var hashed = r.Spec.Kind == JobKind.Restore || (!restore && r.Spec.VerifyHash);
        Stat(3, L.T("Integridade"), hashed ? r.Integrity : "—", !hashed ? UI.Muted : r.IntegrityOk ? UI.Ok : UI.ErrSoft);

        foreach (var m in r.Modules)
        {
            var dot = m.Level switch { StepState.Done => UI.Ok, StepState.Warning => UI.Warn, StepState.Error => UI.ErrSoft, _ => UI.BoxOff };
            var g = new Grid { ColumnSpacing = 12, Padding = new Thickness(18, 11, 18, 11), BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 8, Height = 8, Fill = dot, VerticalAlignment = VerticalAlignment.Center });
            var name = new TextBlock { Text = L.T(m.Name), Foreground = m.Level == StepState.Unavailable ? UI.Muted : UI.Text };
            Grid.SetColumn(name, 1);
            g.Children.Add(name);
            var detail = new TextBlock { Text = m.Detail, FontSize = 12, Foreground = UI.Muted2, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(detail, 2);
            g.Children.Add(detail);
            ModulesPanel.Children.Add(g);
        }

        List<string> warnings;
        lock (r.Warnings) warnings = r.Warnings.ToList();
        if (r.State == RunState.Canceled) warnings.Insert(0, L.T("A tarefa foi cancelada: esta versão está incompleta."));
        if (warnings.Count == 0)
        {
            AttentionTitle.Text = L.T("Tudo certo");
            WarningsPanel.Children.Add(new TextBlock { Text = L.T("Nenhum aviso nesta execução."), Foreground = UI.Muted, FontSize = 13 });
        }
        foreach (var w in warnings.Take(4)) WarningsPanel.Children.Add(WarnCard(w));
        if (warnings.Count > 4)
            WarningsPanel.Children.Add(new TextBlock { Text = L.F("+ {0} avisos no log", warnings.Count - 4), Foreground = UI.Muted, FontSize = 12.5 });

        RebootButton.Visibility = r.RebootRequired ? Visibility.Visible : Visibility.Collapsed;
        FolderButton.Visibility = restore ? Visibility.Collapsed : Visibility.Visible;
    }

    void Stat(int col, string label, string value, Brush color)
    {
        var s = new StackPanel { Spacing = 4 };
        s.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = UI.Muted });
        s.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = color, TextWrapping = TextWrapping.Wrap });
        var b = new Border { Style = (Style)Application.Current.Resources["Card"], Padding = new Thickness(18, 16, 18, 16), Child = s };
        Grid.SetColumn(b, col);
        StatsGrid.Children.Add(b);
    }

    static Border WarnCard(string text)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(new Icon { Data = Icons.Warning, Foreground = UI.Warn });
        var t = new TextBlock { Text = text, FontSize = 13, LineHeight = 19, Foreground = UI.Brush("#ffd89a"), TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(t, 1);
        g.Children.Add(t);
        return new Border { Style = (Style)Application.Current.Resources["WarnCard"], Padding = new Thickness(16, 14, 16, 14), Child = g };
    }

    void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (_r?.LogPath is { Length: > 0 } p && File.Exists(p)) Sys.Open(p);
    }

    void OnFolder(object sender, RoutedEventArgs e)
    {
        if (_r?.VersionPath is { Length: > 0 } p && Directory.Exists(p)) Sys.Open(p);
    }

    void OnDone(object sender, RoutedEventArgs e) => App.Shell.Navigate("home");

    async void OnReboot(object sender, RoutedEventArgs e)
    {
        var ok = await Dialogs.ConfirmAsync(XamlRoot, L.T("Reiniciar o computador?"), L.T("Salve o que estiver aberto em outros programas antes de continuar."), L.T("Reiniciar agora"), L.T("Depois"));
        if (ok) Process.Start(new ProcessStartInfo(Proc.SystemExe("shutdown.exe"), "/r /t 5 /c \"WINbackup: reiniciando para terminar a instalação dos drivers\"") { CreateNoWindow = true, UseShellExecute = false });
    }

    /// <summary>Gera relatorio.html ao lado do backup e abre no navegador.</summary>
    void OnExport(object sender, RoutedEventArgs e)
    {
        if (_r == null) return;
        var r = _r;
        static string H(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"" + L.Culture.Name + "\"><head><meta charset=\"utf-8\"><title>WINbackup · " + L.T("Relatório") + "</title><style>")
          .Append("body{margin:0;background:#0a0f1c;color:#e8f0ff;font:14px 'Segoe UI Variable Text','Segoe UI',system-ui,sans-serif;padding:32px}")
          .Append("h1{font-size:26px;margin:0 0 4px}p.sub{color:#8ea3c7;margin:0 0 20px}.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:18px}")
          .Append(".card{background:#0e1526;border:1px solid #1c2a48;border-radius:12px;padding:16px 18px}.lbl{font-size:12px;color:#8ea3c7}.val{font-size:22px;font-weight:700}")
          .Append("table{width:100%;border-collapse:collapse;background:#0e1526;border:1px solid #1c2a48;border-radius:12px;overflow:hidden;margin-bottom:18px}")
          .Append("td,th{padding:10px 16px;border-bottom:1px solid #16223d;text-align:left}th{color:#8ea3c7;font-weight:400;font-size:12px}")
          .Append(".warn{background:#1d1608;border:1px solid #5c4413;color:#ffd89a;border-radius:12px;padding:12px 16px;margin-bottom:8px}")
          .Append("code{font-family:'Cascadia Mono',Consolas,monospace;font-size:12.5px}</style></head><body>");
        sb.Append($"<h1>{H(TitleText.Text)}</h1><p class=\"sub\">{H(SubText.Text)} · {r.StartedAt:g}</p><div class=\"grid\">");
        foreach (var child in StatsGrid.Children.OfType<Border>())
        {
            var texts = ((StackPanel)child.Child).Children.OfType<TextBlock>().ToList();
            sb.Append($"<div class=\"card\"><div class=\"lbl\">{H(texts[0].Text)}</div><div class=\"val\">{H(texts[1].Text)}</div></div>");
        }
        sb.Append($"</div><table><tr><th>{L.T("Módulo")}</th><th>{L.T("Resultado")}</th></tr>");
        foreach (var m in r.Modules) sb.Append($"<tr><td>{H(L.T(m.Name))}</td><td>{H(m.Detail)}</td></tr>");
        sb.Append("</table>");
        lock (r.Warnings) foreach (var w in r.Warnings) sb.Append($"<div class=\"warn\">{H(w)}</div>");
        sb.Append($"<p class=\"sub\">{L.T("Log completo:")} <code>{H(r.LogPath)}</code></p></body></html>");

        var folder = r.Spec.Kind == JobKind.Backup && Directory.Exists(r.VersionPath) ? r.VersionPath : Path.GetDirectoryName(r.LogPath)!;
        var file = Path.Combine(folder, "relatorio.html");
        File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
        Sys.Open(file);
    }
}
