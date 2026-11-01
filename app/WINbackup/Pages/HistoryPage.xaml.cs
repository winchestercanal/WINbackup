using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

/// <summary>Versões e logs reais, lidos da pasta de destino.</summary>
public sealed partial class HistoryPage : Page
{
    List<LogLine> _log = new();
    string? _logFile;

    public HistoryPage()
    {
        InitializeComponent();
        TabV.Checked += (_, _) => { VersionsTab.Visibility = Visibility.Visible; LogsTab.Visibility = Visibility.Collapsed; };
        TabL.Checked += (_, _) => { VersionsTab.Visibility = Visibility.Collapsed; LogsTab.Visibility = Visibility.Visible; };
        KeepText.Text = Settings.Data.KeepVersions.ToString();
        LoadVersions();
        LoadLog();
    }

    void LoadVersions()
    {
        var versions = Destinations.Versions();
        var mono = (FontFamily)Application.Current.Resources["Mono"];
        if (versions.Count == 0)
        {
            VersionsPanel.Children.Add(new TextBlock { Text = "Nenhuma versão no destino atual ainda.", Foreground = UI.Muted, Margin = new Thickness(18, 14, 18, 14) });
            return;
        }
        for (var i = 0; i < versions.Count; i++)
        {
            var v = versions[i];
            var m = Manifest.Load(v);
            var expiring = i >= Settings.Data.KeepVersions && m?.Kind != "Completo";
            var g = new Grid { ColumnSpacing = 12, Padding = new Thickness(18, 12, 18, 12), Opacity = expiring ? 0.45 : 1,
                BorderBrush = UI.Brush("#16223d"), BorderThickness = new Thickness(0, 0, 0, 1) };
            foreach (var w in new[] { 1.8, 1, 0.9, 1.2, 1, 1.6 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });

            Add(g, 0, new TextBlock { Text = HomePage.VersionDate(v)?.ToString("yyyy-MM-dd HH:mm") ?? Path.GetFileName(v), FontFamily = mono, FontSize = 12.5 });
            Add(g, 1, new TextBlock { Text = m == null ? "?" : m.Completed ? m.Kind : $"{m.Kind} (incompleto)", TextTrimming = TextTrimming.CharacterEllipsis });
            Add(g, 2, new TextBlock { Text = m == null ? "—" : Sys.Bytes(m.BytesStored), Foreground = UI.Muted2 });

            var (hash, color) = m == null || !m.HashChecked ? ("Não verificado", UI.Muted)
                : m.HashMismatches == 0 ? ("SHA-256 conferido", UI.Ok) : ($"{m.HashMismatches} arquivos divergentes", UI.Warn);
            var hs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            hs.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 7, Height = 7, Fill = color, VerticalAlignment = VerticalAlignment.Center });
            hs.Children.Add(new TextBlock { Text = hash, FontSize = 12, Foreground = color });
            Add(g, 3, hs);
            Add(g, 4, new TextBlock { Text = m?.Encrypted == true ? "AES-256" : "Sem criptografia", FontSize = 12, Foreground = m?.Encrypted == true ? UI.NeonSoft : UI.Muted });

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            var open = SmallButton("Abrir");
            open.Click += (_, _) => Sys.Open(v);
            var restore = SmallButton("Restaurar");
            restore.Click += (_, _) => App.Shell.Navigate("restore");
            actions.Children.Add(open);
            actions.Children.Add(restore);
            Add(g, 5, actions);
            VersionsPanel.Children.Add(g);
        }
    }

    static Button SmallButton(string text) => new()
    {
        Style = (Style)Application.Current.Resources["SecondaryButton"], Height = 30, Padding = new Thickness(10, 0, 10, 0),
        FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Normal, CornerRadius = new CornerRadius(7), Content = text,
    };

    static void Add(Grid g, int col, FrameworkElement e)
    {
        e.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(e, col);
        g.Children.Add(e);
    }

    static readonly Regex LineRx = new(@"^\[(?<d>[^\]]+)\] \[(?<l>[A-Z]+)\] (?<m>.*)$");

    void LoadLog()
    {
        _logFile = Destinations.Versions()
            .Select(v => Directory.EnumerateFiles(v, "backup_audit_*.log").FirstOrDefault())
            .FirstOrDefault(f => f != null);
        var logsDir = Path.Combine(Settings.Folder, "logs");
        var restoreLog = Directory.Exists(logsDir) ? Directory.GetFiles(logsDir, "*.log").OrderByDescending(f => f).FirstOrDefault() : null;
        if (restoreLog != null && (_logFile == null || File.GetLastWriteTime(restoreLog) > File.GetLastWriteTime(_logFile))) _logFile = restoreLog;

        if (_logFile == null)
        {
            LogSource.Text = "Nenhum log ainda.";
            return;
        }
        LogSource.Text = _logFile;
        try
        {
            using var fs = new FileStream(_logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            _log = reader.ReadToEnd().Split('\n').Select(l => LineRx.Match(l.TrimEnd('\r'))).Where(m => m.Success)
                .Select(m => new LogLine { Time = m.Groups["d"].Value.Split(' ').Last(), Level = m.Groups["l"].Value, Message = m.Groups["m"].Value })
                .TakeLast(300).ToList();
        }
        catch (IOException ex)
        {
            LogSource.Text = "Não foi possível ler o log: " + ex.Message;
        }
        LogLines.ItemsSource = _log;
    }

    void OnLogFilter(object sender, RoutedEventArgs e)
    {
        if (LogLines == null) return;
        var tag = (string)((FrameworkElement)sender).Tag;
        LogLines.ItemsSource = tag switch
        {
            "info" => _log.Where(l => l.Level is "INFO" or "OK").ToList(),
            "warn" => _log.Where(l => l.Level == "AVISO").ToList(),
            "err" => _log.Where(l => l.Level == "ERRO").ToList(),
            _ => _log,
        };
    }

    void OnOpenLogs(object sender, RoutedEventArgs e)
    {
        var dir = _logFile != null ? Path.GetDirectoryName(_logFile) : Settings.Data.Destination;
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Sys.Open(dir);
    }

    void OnDec(object sender, RoutedEventArgs e) => SetKeep(Settings.Data.KeepVersions - 1);
    void OnInc(object sender, RoutedEventArgs e) => SetKeep(Settings.Data.KeepVersions + 1);

    void SetKeep(int n)
    {
        Settings.Data.KeepVersions = Math.Clamp(n, 1, 30);
        Settings.Save();
        KeepText.Text = Settings.Data.KeepVersions.ToString();
        while (VersionsPanel.Children.Count > 1) VersionsPanel.Children.RemoveAt(1);
        LoadVersions();
    }
}
