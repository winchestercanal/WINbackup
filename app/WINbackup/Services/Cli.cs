using WINbackup.Backup;

namespace WINbackup.Services;

/// <summary>
/// Linha de comando (também usada pelos agendamentos):
///   WINbackup.exe --preset completo [--dest E:\Backup] [--full] [--silent]
///   WINbackup.exe --only files,apps,configs,games,browsers,creators,drivers [--dest E:\Backup] [--silent]
///   WINbackup.exe --restore E:\Backup [--postformat] [--no-reboot] [--silent]
///   WINbackup.exe --schedule &lt;id&gt;   (tarefas do Agendador)
///   WINbackup.exe --remind            (lembrete de 30 dias sem backup)
/// Códigos de saída: 0 ok, 1 terminou com erros, 2 argumentos inválidos, 3 já há uma tarefa rodando.
/// </summary>
public static class Cli
{
    public static bool TryRun(string[] args)
    {
        if (args.Length == 0) return false;
        string? Get(string name)
        {
            var i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
        }
        bool Has(string name) => args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (Has("--remind"))
        {
            Remind();
            return true;
        }

        var silent = Has("--silent");
        JobSpec? spec;
        ScheduleDef? schedule = null;
        if (Get("--schedule") is { } id)
        {
            schedule = Settings.Data.Schedules.FirstOrDefault(s => s.Id == id);
            if (schedule == null) { Exit(2); return true; }
            spec = FromSchedule(schedule);
            if (spec == null) { Exit(0); return true; }
            silent = true;
        }
        else if (Get("--preset") is { } slug)
        {
            var index = Array.FindIndex(Presets.All, p => p.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (index < 0) { Fail($"Preset desconhecido: {slug}. Use: {string.Join(", ", Presets.All.Select(p => p.Slug))}."); return true; }
            spec = Presets.Build(index, Get("--dest"), Has("--full") ? false : null);
        }
        else if (Get("--only") is { } only)
        {
            spec = Only(only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), Get("--dest"));
            if (spec == null) { Fail("Módulos válidos em --only: files, creators, apps, configs, games, browsers, drivers."); return true; }
        }
        else if (Get("--restore") is { } from)
        {
            var plan = RestorePlan.Latest(from);
            if (plan.Empty) { Fail($"Nenhum backup do WINbackup em {from}."); return true; }
            spec = new JobSpec
            {
                Kind = JobKind.Restore, Title = "Pós-formatação", RestoreSources = plan.Sources(),
                Target = RestoreTarget.Original, Conflict = ConflictMode.Skip, NoReboot = Has("--no-reboot"),
            };
        }
        else
        {
            return false;
        }

        if (spec.Kind == JobKind.Backup && string.IsNullOrEmpty(spec.Destination)) { Fail("Sem destino: use --dest ou escolha o destino no app."); return true; }
        if (BackupRunner.IsAnotherRunning()) { Exit(3); return true; }

        App.Shell.Activate();
        if (silent) App.Shell.Minimize();
        App.Shell.Run(spec);
        var runner = BackupRunner.Current!;
        runner.StateChanged += () =>
        {
            if (!runner.IsFinished) return;
            if (!silent) return;
            var code = runner.State == RunState.Completed && runner.ErrorCount == 0 ? 0 : 1;
            // Pós-formatação sem perguntas: reinicia se os drivers pedirem (60 s para cancelar com "shutdown /a")
            if (runner.RebootRequired && !runner.Spec.NoReboot)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Proc.SystemExe("shutdown.exe"),
                    "/r /t 60 /c \"WINbackup: reiniciando para terminar a instalação dos drivers\"") { CreateNoWindow = true, UseShellExecute = false });
            Exit(code, TimeSpan.FromSeconds(10));
        };
        return true;
    }

    /// <summary>Monta a tarefa de um agendamento, ou null se não é hora de rodar.</summary>
    static JobSpec? FromSchedule(ScheduleDef s)
    {
        string? dest = null;
        if (s.Kind == "drive")
        {
            // O evento de disco conectado chega várias vezes: só segue se for o disco certo e não rodou agora há pouco
            var root = s.VolumeId == null ? null : Scheduler.RootOf(s.VolumeId);
            if (root == null) return null;
            if (s.LastRun is { } last && DateTime.Now - last < TimeSpan.FromMinutes(30)) return null;
            dest = Path.Combine(root, s.VolumeSubPath ?? "Backup");
        }
        else if (s.Kind == "logon" && s.LastRun?.Date == DateTime.Today)
        {
            return null;
        }
        var index = Array.FindIndex(Presets.All, p => p.Slug == s.Preset);
        if (index < 0) return null;
        s.LastRun = DateTime.Now;
        Settings.Save();
        return Presets.Build(index, dest, s.Incremental);
    }

    static JobSpec? Only(string[] modules, string? dest)
    {
        var valid = new[] { "files", "creators", "apps", "configs", "games", "browsers", "drivers" };
        if (modules.Length == 0 || modules.Any(m => !valid.Contains(m, StringComparer.OrdinalIgnoreCase))) return null;
        bool On(string m) => modules.Contains(m, StringComparer.OrdinalIgnoreCase);
        var folders = new List<FolderSpec>();
        if (On("files")) folders.AddRange(PersonalFolders.Selected());
        if (On("creators")) folders.AddRange(CreatorApps.SelectedFolders());
        if (On("configs")) folders.AddRange(ConfigsModule.SelectedAppFolders());
        if (On("games")) folders.AddRange(GamesModule.SelectedFolders());
        if (On("browsers")) folders.AddRange(BrowsersModule.SelectedFolders());
        return new JobSpec
        {
            Title = "Linha de comando: " + string.Join(", ", modules), FilesLabel = "Arquivos e configurações",
            Destination = dest ?? Settings.Data.Destination, Folders = folders,
            Exclusions = Settings.Data.Exclusions.Concat(CreatorApps.Exclusions).Concat(ConfigsModule.Exclusions).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Programs = On("apps"), Drivers = On("drivers"), Browsers = On("browsers"), Games = On("games"),
            WindowsKeys = On("configs") ? Presets.WindowsKeys() : [],
        };
    }

    /// <summary>Lembrete: avisa se o último backup tem mais de 30 dias. Roda sem janela e fecha sozinho.</summary>
    static void Remind()
    {
        var days = DaysSinceLastBackup();
        if (Settings.Data.NotifyReminder && days is null or > 30)
            Notifier.Show(days == null ? "Você ainda não fez backup" : $"Faz {days} dias desde o último backup",
                "Abra o WINbackup para salvar seus arquivos antes que algo aconteça.");
        Exit(0, TimeSpan.FromSeconds(20));
    }

    public static int? DaysSinceLastBackup()
    {
        var last = Destinations.Versions().Select(v => Pages.HomePage.VersionDate(v)).FirstOrDefault(d => d != null);
        return last == null ? null : (int)(DateTime.Now - last.Value).TotalDays;
    }

    static void Fail(string message)
    {
        App.Shell.Activate();
        App.Shell.DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(500);
            await Controls.Dialogs.InfoAsync(App.Shell.Content.XamlRoot, "Linha de comando", message);
        });
    }

    static void Exit(int code, TimeSpan? delay = null)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay ?? TimeSpan.Zero);
            Notifier.Shutdown();
            Environment.Exit(code);
        });
    }
}
