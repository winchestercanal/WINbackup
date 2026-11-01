using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using WINbackup.Backup;
using WINbackup.Controls;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Pages;

public sealed partial class ProgressPage : Page
{
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly BackupRunner? _runner = BackupRunner.Current;
    long _lastBytes;
    DateTime _lastTick = DateTime.Now;
    double _speed;

    public ProgressPage()
    {
        InitializeComponent();
        if (_runner == null)
        {
            TitleText.Text = "Nenhuma tarefa em andamento";
            StateLabel.Text = "";
            SubText.Text = "Comece um backup pelo Início ou pelas telas do menu Backup.";
            PauseButton.Visibility = CancelButton.Visibility = OpenLogButton.Visibility = HashBadge.Visibility = Visibility.Collapsed;
            return;
        }

        TitleText.Text = _runner.Spec.Title;
        HashBadge.Visibility = _runner.Spec.Kind == JobKind.Backup && _runner.Spec.Folders.Count > 0 && _runner.Spec.VerifyHash
            ? Visibility.Visible : Visibility.Collapsed;
        StepsRepeater.ItemsSource = _runner.Steps;
        LogList.ItemsSource = _runner.Tail;
        _lastBytes = _runner.BytesMoved;

        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => { _runner.StateChanged += OnState; _timer.Start(); OnState(); Tick(); };
        Unloaded += (_, _) => { _runner.StateChanged -= OnState; _timer.Stop(); };
    }

    void OnState()
    {
        var r = _runner!;
        var (label, brush) = r.State switch
        {
            RunState.Paused => ("PAUSADO", UI.Warn),
            RunState.Completed => ("CONCLUÍDO", UI.Ok),
            RunState.Canceled => ("CANCELADO", UI.Warn),
            RunState.Failed => ("FALHOU", UI.ErrSoft),
            _ => ("EM ANDAMENTO", UI.Neon),
        };
        StateLabel.Text = label;
        StateLabel.Foreground = brush;
        Bar.Background = brush;
        PauseButton.Content = r.State == RunState.Paused ? "Continuar" : "Pausar";
        var done = r.IsFinished;
        PauseButton.Visibility = CancelButton.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
        ReportButton.Visibility = done ? Visibility.Visible : Visibility.Collapsed;
        if (done)
        {
            Tick();
            _timer.Stop();
            // Abre o relatório sozinho, como no fluxo do protótipo
            if (r.State != RunState.Canceled) App.Shell.Navigate("report");
        }
    }

    void Tick()
    {
        var r = _runner!;
        var pct = r.State == RunState.Completed ? 100 : Math.Clamp(r.Percent, 0, 100);
        PercentText.Text = $"{(int)pct}%";
        DoneCol.Width = new GridLength(pct, GridUnitType.Star);
        LeftCol.Width = new GridLength(100 - pct, GridUnitType.Star);

        NowText.Inlines.Clear();
        if (r.IsFinished)
        {
            NowText.Inlines.Add(new Run { Text = r.State == RunState.Completed ? "Tudo pronto" : "Parado" });
        }
        else
        {
            NowText.Inlines.Add(new Run { Text = r.Activity == "integridade" ? "Conferindo " : r.Spec.IsRestore ? "Restaurando " : "Copiando " });
            NowText.Inlines.Add(new Run { Text = r.Activity, FontWeight = FontWeights.Bold });
            if (!string.IsNullOrEmpty(r.CurrentFile))
            {
                NowText.Inlines.Add(new Run { Text = " · " });
                NowText.Inlines.Add(new Run { Text = r.CurrentFile, FontFamily = (Microsoft.UI.Xaml.Media.FontFamily)Application.Current.Resources["Mono"], FontSize = 12.5, Foreground = UI.Muted2 });
            }
        }

        // Velocidade suavizada
        var now = DateTime.Now;
        var dt = (now - _lastTick).TotalSeconds;
        var moved = r.BytesMoved;
        if (dt > 0 && r.State == RunState.Running)
            _speed = _speed * 0.7 + (moved - _lastBytes) / dt * 0.3;
        _lastBytes = moved;
        _lastTick = now;

        var phaseTotal = r.PhaseTotal;
        BytesText.Text = phaseTotal > 0
            ? $"{Sys.Bytes(r.PhaseDone)} de {Sys.Bytes(phaseTotal)} · {_speed / 1048576:0.0} MB/s"
            : $"Decorrido: {Sys.Duration(r.Elapsed)}";
        if (r.IsFinished) EtaText.Text = $"Duração: {Sys.Duration(r.Elapsed)}";
        else if (r.State == RunState.Paused) EtaText.Text = "Pausado";
        else if (pct is > 1 and < 100)
            EtaText.Text = $"Tempo restante: {Sys.Duration(TimeSpan.FromSeconds(r.Elapsed.TotalSeconds * (100 - pct) / pct))}";
        else EtaText.Text = "Tempo restante: calculando…";
    }

    void OnPause(object sender, RoutedEventArgs e)
    {
        if (_runner!.State == RunState.Paused) _runner.Resume();
        else _runner.Pause();
    }

    async void OnCancel(object sender, RoutedEventArgs e)
    {
        var ok = await Dialogs.ConfirmAsync(XamlRoot, "Cancelar agora?",
            "O que já foi copiado fica no destino, mas esta versão fica incompleta.", "Cancelar tarefa", "Continuar", danger: true);
        if (ok) _runner!.Cancel();
    }

    void OnOpenLog(object sender, RoutedEventArgs e)
    {
        if (_runner?.LogPath is { Length: > 0 } p && File.Exists(p)) Sys.Open(p);
    }

    void OnReport(object sender, RoutedEventArgs e) => App.Shell.Navigate("report");
}
