using System.Globalization;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace WINbackup.Services;

/// <summary>Um agendamento: quando roda, qual preset e opções. Guardado nas configurações do app.</summary>
public class ScheduleDef
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>"time", "drive" ou "logon".</summary>
    public string Kind { get; set; } = "time";
    public List<DayOfWeek> Days { get; set; } = new();
    public string Time { get; set; } = "20:00";
    public string Preset { get; set; } = "documentos";
    /// <summary>Disco do gatilho, pelo ID do volume (a letra pode mudar).</summary>
    public string? VolumeId { get; set; }
    public string? VolumeLabel { get; set; }
    /// <summary>Pasta de destino dentro do disco do gatilho ("Backup").</summary>
    public string? VolumeSubPath { get; set; }
    public bool Incremental { get; set; } = true;
    public bool Wake { get; set; }
    public bool AcOnly { get; set; } = true;
    public DateTime? LastRun { get; set; }

    public string TaskName => $"Backup {Id}";

    static readonly string[] DayNames = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];
    static readonly string[] DayFull = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];

    public string When => Kind switch
    {
        "drive" => $"Ao conectar {VolumeLabel ?? "o disco"}",
        "logon" => "Ao entrar no Windows",
        _ when Days.Count == 7 => $"Todo dia, {Time}",
        _ when Days.Count == 1 => $"Toda {DayFull[(int)Days[0]]}, {Time}",
        _ => $"{string.Join(", ", Days.OrderBy(d => d).Select(d => DayNames[(int)d]))}, {Time}",
    };
}

/// <summary>Cria, liga/desliga e remove as tarefas do WINbackup no Agendador de Tarefas (pasta \WINbackup).</summary>
public static partial class Scheduler
{
    const string FolderName = "WINbackup";

    static dynamic Folder()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
        service.Connect();
        dynamic root = service.GetFolder("\\");
        try { return service.GetFolder("\\" + FolderName); }
        catch (COMException) { return root.CreateFolder(FolderName); }
    }

    public static void Register(ScheduleDef def)
    {
        // 6 = criar ou atualizar; 3 = token interativo (roda com o usuário logado, sem pedir senha)
        Folder().RegisterTask(def.TaskName, Xml(def), 6, null, null, 3);
    }

    /// <summary>Lembrete de 30 dias sem backup: roda ao entrar no Windows, sem janela.</summary>
    public static void SetReminder(bool on)
    {
        try
        {
            if (on) Folder().RegisterTask("Lembrete", Xml(null), 6, null, null, 3);
            else Folder().DeleteTask("Lembrete", 0);
        }
        catch (Exception ex) when (ex is COMException or FileNotFoundException) { }
    }

    public static void Delete(ScheduleDef def)
    {
        try { Folder().DeleteTask(def.TaskName, 0); }
        catch (Exception ex) when (ex is COMException or FileNotFoundException) { }
    }

    public static void SetEnabled(ScheduleDef def, bool on)
    {
        dynamic task = Folder().GetTask(def.TaskName);
        task.Enabled = on;
    }

    /// <summary>Estado da tarefa no Windows: existe, ligada e próxima execução (null em gatilhos de evento/entrada).</summary>
    public static (bool Exists, bool Enabled, DateTime? Next) State(ScheduleDef def)
    {
        try
        {
            dynamic task = Folder().GetTask(def.TaskName);
            DateTime next = task.NextRunTime;
            return (true, task.Enabled, next.Year > 2000 ? next : null);
        }
        catch (Exception) { return (false, false, null); }
    }

    /// <summary>def null = tarefa do lembrete.</summary>
    static string Xml(ScheduleDef? def)
    {
        var user = SecurityElement.Escape($"{Environment.UserDomainName}\\{Environment.UserName}");
        var exe = SecurityElement.Escape(Environment.ProcessPath!);
        var args = def == null ? "--remind" : $"--schedule {def.Id}";
        var triggers = new StringBuilder();
        if (def == null || def.Kind == "logon")
        {
            triggers.Append($"<LogonTrigger><UserId>{user}</UserId><Delay>PT{(def == null ? 2 : 5)}M</Delay></LogonTrigger>");
        }
        else if (def.Kind == "drive")
        {
            // Partition/Diagnostic 1006: um disco foi conectado (dispara algumas vezes por conexão; o app ignora as repetições)
            var query = "<QueryList><Query Id=\"0\" Path=\"Microsoft-Windows-Partition/Diagnostic\"><Select Path=\"Microsoft-Windows-Partition/Diagnostic\">*[System[(EventID=1006)]]</Select></Query></QueryList>";
            triggers.Append($"<EventTrigger><Subscription>{SecurityElement.Escape(query)}</Subscription><Delay>PT20S</Delay></EventTrigger>");
        }
        else
        {
            var time = TimeSpan.TryParse(def.Time, CultureInfo.InvariantCulture, out var t) ? t : new TimeSpan(20, 0, 0);
            var start = DateTime.Today.Add(time).ToString("yyyy-MM-ddTHH:mm:ss");
            var days = string.Concat(def.Days.Select(d => $"<{d}/>"));
            triggers.Append($"<CalendarTrigger><StartBoundary>{start}</StartBoundary><ScheduleByWeek><DaysOfWeek>{days}</DaysOfWeek><WeeksInterval>1</WeeksInterval></ScheduleByWeek></CalendarTrigger>");
        }

        var ac = def?.AcOnly == true ? "true" : "false";
        var wake = def?.Wake == true ? "true" : "false";
        var description = SecurityElement.Escape(def == null ? "WINbackup: lembrete de backup" : $"WINbackup: {def.When} · preset {def.Preset}");
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>{description}</Description></RegistrationInfo>
              <Triggers>{triggers}</Triggers>
              <Principals><Principal id="Author"><UserId>{user}</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>{ac}</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <StartWhenAvailable>true</StartWhenAvailable>
                <WakeToRun>{wake}</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Enabled>true</Enabled>
              </Settings>
              <Actions Context="Author"><Exec><Command>{exe}</Command><Arguments>{args}</Arguments></Exec></Actions>
            </Task>
            """;
    }

    // ---------- Volumes ----------

    /// <summary>"\\?\Volume{GUID}\" do disco de um caminho (E:\Backup → volume de E:).</summary>
    public static string? VolumeId(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\")) return null;
        var sb = new char[64];
        return GetVolumeNameForVolumeMountPoint(root, sb, sb.Length) ? new string(sb).TrimEnd('\0') : null;
    }

    /// <summary>Letra atual de um volume (ou null se o disco não estiver conectado).</summary>
    public static string? RootOf(string volumeId)
    {
        var buffer = new char[1024];
        if (!GetVolumePathNamesForVolumeName(volumeId, buffer, buffer.Length, out _)) return null;
        var first = new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first != null && Directory.Exists(first) ? first : null;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetVolumeNameForVolumeMountPointW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeNameForVolumeMountPoint(string mountPoint, [Out] char[] volumeName, int length);

    [LibraryImport("kernel32.dll", EntryPoint = "GetVolumePathNamesForVolumeNameW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumePathNamesForVolumeName(string volumeName, [Out] char[] names, int length, out int returned);
}
