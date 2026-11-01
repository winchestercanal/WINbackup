using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Backup;

public enum RunState { Running, Paused, Completed, Canceled, Failed }

public record ModuleResult(string Name, string Detail, StepState Level);

/// <summary>
/// Executa uma tarefa em segundo plano, com etapas, progresso, pausa e cancelamento,
/// e grava um log por execução. As telas Em andamento e Relatório leem daqui.
/// </summary>
public sealed class BackupRunner
{
    public static BackupRunner? Current { get; private set; }

    readonly DispatcherQueue _ui;
    readonly CancellationTokenSource _cts = new();
    readonly ManualResetEventSlim _gate = new(true);
    readonly object _logLock = new();
    readonly Dictionary<StepItem, (double Weight, double Fraction)> _progress = new();
    StreamWriter? _logFile;
    DateTime _pausedAt;
    TimeSpan _pausedTotal;

    public JobSpec Spec { get; }
    public ObservableCollection<StepItem> Steps { get; } = new();
    /// <summary>Últimas linhas do log, para a tela de progresso.</summary>
    public ObservableCollection<LogLine> Tail { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<ModuleResult> Modules { get; } = new();

    public RunState State { get; private set; } = RunState.Running;
    public bool IsFinished => State is RunState.Completed or RunState.Canceled or RunState.Failed;
    public DateTime StartedAt { get; } = DateTime.Now;
    public DateTime EndedAt { get; private set; }
    public string VersionPath { get; private set; } = "";
    public string LogPath { get; private set; } = "";

    // Lidos pela interface a cada atualização
    public volatile string Activity = "Preparando…";
    public volatile string CurrentFile = "";
    long _bytesMoved, _phaseDone, _phaseTotal;
    public long BytesMoved => Interlocked.Read(ref _bytesMoved);
    public long PhaseDone => Interlocked.Read(ref _phaseDone);
    public long PhaseTotal => Interlocked.Read(ref _phaseTotal);
    public double Percent { get; private set; }

    // Resumo para o relatório
    public int ItemsSaved;
    public long StoredBytes;
    public string Integrity = "Desativada";
    public bool IntegrityOk = true;
    public bool RebootRequired;
    public int ErrorCount, WarningCount;
    /// <summary>Já contou para o lembrete de doação (o relatório pode ser aberto mais de uma vez).</summary>
    public bool CountedForDonation;
    public bool RebootAsked;

    public event Action? StateChanged;

    BackupRunner(JobSpec spec)
    {
        Spec = spec;
        _ui = DispatcherQueue.GetForCurrentThread();
    }

    public static BackupRunner Start(JobSpec spec)
    {
        var r = new BackupRunner(spec);
        Current = r;
        r.BuildSteps();
        _ = Task.Run(r.RunAsync);
        return r;
    }

    public TimeSpan Elapsed
    {
        get
        {
            var end = IsFinished ? EndedAt : DateTime.Now;
            var paused = _pausedTotal + (State == RunState.Paused ? DateTime.Now - _pausedAt : TimeSpan.Zero);
            return end - StartedAt - paused;
        }
    }

    // ---------- Controle ----------

    public void Pause()
    {
        if (State != RunState.Running) return;
        _gate.Reset();
        _pausedAt = DateTime.Now;
        SetState(RunState.Paused);
        Info("Pausado pelo usuário");
        foreach (var s in Steps) s.Paused = true;
    }

    public void Resume()
    {
        if (State != RunState.Paused) return;
        _pausedTotal += DateTime.Now - _pausedAt;
        SetState(RunState.Running);
        foreach (var s in Steps) s.Paused = false;
        Info("Continuando");
        _gate.Set();
    }

    public void Cancel()
    {
        if (IsFinished) return;
        if (State == RunState.Paused) _pausedTotal += DateTime.Now - _pausedAt;
        _cts.Cancel();
        _gate.Set();
    }

    /// <summary>Chamado pelos módulos entre um bloco e outro: segura enquanto pausado.</summary>
    public void Checkpoint(CancellationToken token)
    {
        _gate.Wait(token);
        token.ThrowIfCancellationRequested();
    }

    // ---------- Progresso ----------

    public void AddBytes(long n) => Interlocked.Add(ref _bytesMoved, n);

    public void SetPhase(long done, long total)
    {
        Interlocked.Exchange(ref _phaseDone, done);
        Interlocked.Exchange(ref _phaseTotal, total);
    }

    public void Progress(StepItem step, double fraction)
    {
        lock (_progress)
        {
            if (!_progress.TryGetValue(step, out var p)) return;
            _progress[step] = (p.Weight, Math.Clamp(fraction, 0, 1));
            var total = _progress.Values.Sum(v => v.Weight);
            Percent = total <= 0 ? 0 : _progress.Values.Sum(v => v.Weight * v.Fraction) / total * 100;
        }
    }

    public void Step(StepItem step, StepState state, string status)
    {
        if (state is StepState.Done or StepState.Warning or StepState.Error or StepState.Skipped) Progress(step, 1);
        _ui.TryEnqueue(() => { step.State = state; step.Status = status; });
    }

    // ---------- Log ----------

    public void Info(string msg) => Write("INFO", msg);
    public void Ok(string msg) => Write("OK", msg);
    public void Warn(string msg, bool report = true)
    {
        Interlocked.Increment(ref WarningCount);
        Write("AVISO", msg);
        if (report) lock (Warnings) if (Warnings.Count < 50) Warnings.Add(msg);
    }
    public void Error(string msg)
    {
        Interlocked.Increment(ref ErrorCount);
        Write("ERRO", msg);
        lock (Warnings) if (Warnings.Count < 50) Warnings.Add(msg);
    }

    void Write(string level, string msg)
    {
        var now = DateTime.Now;
        lock (_logLock)
        {
            try { _logFile?.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss}] [{level}] {msg}"); } catch { }
        }
        var line = new LogLine { Time = now.ToString("HH:mm:ss"), Level = level, Message = msg };
        _ui.TryEnqueue(() =>
        {
            Tail.Add(line);
            while (Tail.Count > 6) Tail.RemoveAt(0);
        });
    }

    void OpenLog(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        LogPath = path;
        _logFile = new StreamWriter(path, append: true, System.Text.Encoding.UTF8) { AutoFlush = true };
    }

    void SetState(RunState s)
    {
        State = s;
        _ui.TryEnqueue(() => StateChanged?.Invoke());
    }

    // ---------- Execução ----------

    StepItem? _vss, _files, _drivers, _verify, _prep, _install, _programs;

    void BuildSteps()
    {
        StepItem Add(string name, string module, double weight)
        {
            var s = new StepItem { Name = name, Module = module };
            Steps.Add(s);
            if (weight > 0) _progress[s] = (weight, 0);
            return s;
        }

        if (Spec.Kind == JobKind.RestoreDrivers)
        {
            _prep = Add("Preparar arquivos", "Drivers", 15);
            _install = Add("Instalar drivers (PnPUtil)", "Drivers", 85);
            return;
        }
        if (Spec.Kind == JobKind.Restore)
        {
            if (Spec.RestoreSources.Any(s => s.Drivers))
            {
                _prep = Add("Preparar drivers", "Drivers", 3);
                _install = Add("Instalar drivers (PnPUtil)", "Drivers", 17);
            }
            if (Spec.InstallPackages != null || Spec.RestoreSources.Any(s => s.Programs))
                _programs = Add("Programas (winget)", "Programas", 40);
            foreach (var name in Spec.Unavailable) Unavailable(Add(name, name, 0));
            if (Spec.RestoreSources.Any(s => s.Folders == null || s.Folders.Count > 0 || s.Files?.Count > 0))
                _files = Add("Arquivos e configurações", "Arquivos", 80);
            return;
        }

        var files = Spec.Folders.Count > 0;
        if (files && Spec.UseShadowCopy) _vss = Add("Shadow Copy (VSS)", Spec.FilesLabel, 0);
        if (Spec.Programs) _programs = Add("Programas (winget)", "Programas", 10);
        if (Spec.Unavailable.Contains("Configurações")) Unavailable(Add("Configurações", "Configurações", 0));
        if (files) _files = Add(Spec.FilesLabel, Spec.FilesLabel, 70);
        if (Spec.Unavailable.Contains("Saves de jogos")) Unavailable(Add("Saves de jogos", "Saves de jogos", 0));
        if (Spec.Drivers) _drivers = Add("Drivers", "Drivers", 20);
        if (files && Spec.VerifyHash) _verify = Add("Verificar integridade (SHA-256)", Spec.FilesLabel, 25);
    }

    static void Unavailable(StepItem s)
    {
        s.State = StepState.Unavailable;
        s.Status = "Em breve nesta versão";
    }

    async Task RunAsync()
    {
        var token = _cts.Token;
        try
        {
            if (Spec.Kind == JobKind.RestoreDrivers)
                await RunRestoreDriversAsync(token);
            else if (Spec.Kind == JobKind.Restore)
                await RunRestoreAsync(token);
            else
                await RunBackupAsync(token);

            EndedAt = DateTime.Now;
            Info($"Concluído em {Sys.Duration(Elapsed)}");
            SetState(RunState.Completed);
        }
        catch (OperationCanceledException)
        {
            EndedAt = DateTime.Now;
            Warn("Cancelado pelo usuário", report: false);
            foreach (var s in Steps.Where(s => s.State is StepState.Running or StepState.Waiting))
                Step(s, StepState.Skipped, "Cancelado");
            SetState(RunState.Canceled);
        }
        catch (Exception ex)
        {
            EndedAt = DateTime.Now;
            CrashLog.Write(ex);
            Error("Falha inesperada: " + ex.Message);
            foreach (var s in Steps.Where(s => s.State == StepState.Running))
                Step(s, StepState.Error, "Falhou");
            SetState(RunState.Failed);
        }
        finally
        {
            lock (_logLock) { _logFile?.Dispose(); _logFile = null; }
            Notify();
        }
    }

    async Task RunBackupAsync(CancellationToken token)
    {
        if (string.IsNullOrEmpty(Spec.Destination))
            throw new InvalidOperationException("Escolha o destino dos backups no menu lateral.");

        var stamp = StartedAt.ToString("yyyy-MM-dd_HH-mm-ss");
        VersionPath = Path.Combine(Spec.Destination, "WINbackup_" + stamp);
        Directory.CreateDirectory(VersionPath);
        OpenLog(Path.Combine(VersionPath, $"backup_audit_{stamp}.log"));
        Info($"{Spec.Title} · destino {VersionPath}");
        if (!Sys.IsAdmin) Warn("O app não está rodando como administrador: alguns arquivos e os drivers podem falhar.");

        var manifest = new Manifest { CreatedAt = StartedAt, Title = Spec.Title };
        manifest.Save(VersionPath);

        if (_files != null)
        {
            manifest.Modules.Add(Spec.FilesLabel);
            await FilesModule.RunAsync(this, _files, _vss, _verify, manifest, token);
        }
        if (_programs != null)
        {
            manifest.Modules.Add("Programas");
            await ProgramsModule.BackupAsync(this, _programs, manifest, token);
        }
        if (Spec.Browsers)
        {
            var browsers = BrowsersModule.Detect();
            BrowsersModule.WriteExtensionList(VersionPath, browsers);
            if (Settings.Data.PasswordCsvs.Count == 0 && browsers.Any(b => b.Installed))
                Warn("Senhas dos navegadores não foram exportadas. Faça a exportação guiada em Navegadores antes de formatar.");
        }
        if (_drivers != null)
        {
            manifest.Modules.Add("Drivers");
            await DriversModule.BackupAsync(this, _drivers, manifest, token);
        }
        foreach (var name in Spec.Unavailable)
            Modules.Add(new ModuleResult(name, "Ainda não disponível nesta versão", StepState.Unavailable));

        manifest.BytesStored = StoredBytes;
        manifest.Completed = true;
        manifest.Save(VersionPath);
        Percent = 100;
    }

    async Task RunRestoreAsync(CancellationToken token)
    {
        var stamp = StartedAt.ToString("yyyy-MM-dd_HH-mm-ss");
        OpenLog(Path.Combine(Settings.Folder, "logs", $"restaurar_{stamp}.log"));
        foreach (var s in Spec.RestoreSources) Info($"Restaurando de {s.VersionPath}");
        Info(Spec.Target switch
        {
            RestoreTarget.Folder => $"Destino: outra pasta ({Spec.TargetPath})",
            RestoreTarget.OtherUser => $"Destino: outro usuário ({Spec.TargetPath})",
            _ => "Destino: locais originais",
        } + $" · se já existir: {Spec.Conflict switch { ConflictMode.Overwrite => "substituir", ConflictMode.KeepBoth => "manter os dois", _ => "pular" }}");
        if (!Sys.IsAdmin) Warn("O app não está rodando como administrador: drivers e pastas protegidas podem falhar.");
        await RestoreModule.RunAsync(this, _files, _prep, _install, _programs, token);
        foreach (var name in Spec.Unavailable)
            Modules.Add(new ModuleResult(name, "Ainda não disponível nesta versão", StepState.Unavailable));
        Percent = 100;
    }

    async Task RunRestoreDriversAsync(CancellationToken token)
    {
        var stamp = StartedAt.ToString("yyyy-MM-dd_HH-mm-ss");
        OpenLog(Path.Combine(Settings.Folder, "logs", $"restaurar_drivers_{stamp}.log"));
        Info($"Restaurando drivers de {Spec.DriverSource}");
        await DriversModule.RestoreAsync(this, _prep!, _install!, Spec.DriverSource!, token);
        Percent = 100;
    }

    void Notify()
    {
        var failed = State == RunState.Failed || ErrorCount > 0;
        if (State == RunState.Canceled) return;
        if (failed ? !Settings.Data.NotifyErrors : !Settings.Data.NotifyDone) return;
        var what = Spec.IsRestore ? "Restauração" : "Backup";
        var title = State == RunState.Failed ? $"{what} falhou"
            : WarningCount + ErrorCount > 0 ? $"{what} concluído com {WarningCount + ErrorCount} avisos" : $"{what} concluído";
        var body = Spec.IsRestore
            ? (RebootRequired ? "Reinicie o computador para terminar." : "Drivers instalados.")
            : $"{Sys.Bytes(StoredBytes)} salvos em {Spec.Destination}" + (IntegrityOk && Spec.VerifyHash ? " · integridade conferida" : "");
        Notifier.Show(title, body);
    }
}
