using System.Text.Json;

namespace WINbackup.Services;

public class SettingsData
{
    public string Destination { get; set; } = "";
    public int Preset { get; set; }

    // Arquivos pessoais
    public string FilesMode { get; set; } = "C";
    public Dictionary<string, bool> FolderSelection { get; set; } = new();
    public List<string> CustomFolders { get; set; } = new();
    public List<string> Exclusions { get; set; } = ["node_modules", "*.tmp", "Cache", "Thumbs.db", "$RECYCLE.BIN", "*.iso"];
    public bool DownloadCloudFiles { get; set; } = true;
    public bool UseShadowCopy { get; set; } = true;
    public bool Incremental { get; set; } = true;
    public bool KeepAcl { get; set; }

    // Programas: IDs do winget que o usuário desmarcou
    public List<string> ProgramsOff { get; set; } = new();

    // Navegadores: "chrome" (navegador inteiro) ou "chrome:Profile 1" desmarcados; .csv de senhas exportados
    public List<string> BrowsersOff { get; set; } = new();
    public List<string> PasswordCsvs { get; set; } = new();

    // Configurações: itens desmarcados ("wifi", "vscode"…) e pastas extras
    public List<string> ConfigsOff { get; set; } = new();
    public List<string> ConfigCustomFolders { get; set; } = new();

    // Saves de jogos
    public Dictionary<string, bool> GamesSelection { get; set; } = new();
    public List<string> GameCustomFolders { get; set; } = new();
    public bool SkipCloudGames { get; set; } = true;

    // Agendamentos
    public List<ScheduleDef> Schedules { get; set; } = new();

    // Criadores
    public Dictionary<string, bool> CreatorSelection { get; set; } = new();
    public List<string> CreatorCustomFolders { get; set; } = new();
    public bool CreatorPlugins { get; set; } = true;

    // Doação
    public int SuccessfulBackups { get; set; }
    public bool DonationReminderOff { get; set; }

    // Opções
    public bool VerifyHash { get; set; } = true;
    public bool NotifyDone { get; set; } = true;
    public bool NotifyErrors { get; set; } = true;
    public bool NotifyReminder { get; set; } = true;
    public int KeepVersions { get; set; } = 5;

    // Aparência: tema ("winchester", "neutral", "oled", "light") e idioma ("auto", "pt", "en", "es")
    public string Theme { get; set; } = "winchester";
    public string Language { get; set; } = "auto";
}

/// <summary>Configurações do app em %APPDATA%\WINbackup\settings.json.</summary>
public static class Settings
{
    public static SettingsData Data { get; private set; } = new();
    public const string PortableMarker = "portable.txt";

    /// <summary>Modo portátil: portable.txt ao lado do .exe guarda tudo em Dados\, sem tocar no PC.</summary>
    public static bool IsPortable => File.Exists(Path.Combine(AppContext.BaseDirectory, PortableMarker));

    // WINBACKUP_DATA troca a pasta (testes); portable.txt usa a pasta Dados ao lado do .exe
    public static string Folder { get; } = Environment.GetEnvironmentVariable("WINBACKUP_DATA") is { Length: > 0 } custom
        ? custom
        : IsPortable ? Path.Combine(AppContext.BaseDirectory, "Dados")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WINbackup");
    static string FilePath => Path.Combine(Folder, "settings.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    /// <summary>Disparado quando o destino dos backups muda.</summary>
    public static event Action? DestinationChanged;

    public static void Load()
    {
        try
        {
            if (File.Exists(FilePath))
                Data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath)) ?? new();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            Data = new();
        }

        if (string.IsNullOrEmpty(Data.Destination))
            Data.Destination = Destinations.Candidates().FirstOrDefault()?.Path ?? "";
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Data, Json));
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
        }
    }

    public static void SetDestination(string path)
    {
        Data.Destination = path;
        Save();
        DestinationChanged?.Invoke();
    }
}
