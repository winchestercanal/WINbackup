using System.Text.RegularExpressions;
using Microsoft.Win32;
using WINbackup.Services;

namespace WINbackup.Backup;

public record GameSave(string Key, string Name, string Path, string Source, bool Cloud);

/// <summary>
/// Saves de jogos: procura nas pastas mais comuns (My Games, Saved Games, Unreal, Unity, Steam, Ubisoft)
/// e trata cada jogo como uma pasta do backup, no grupo "Saves de jogos".
/// </summary>
public static partial class GamesModule
{
    /// <summary>Pastas em LocalLow que não são jogos.</summary>
    static readonly string[] NotGames =
    [
        "Microsoft", "Intel", "Sun", "Oracle", "Adobe", "NVIDIA", "Google", "Mozilla", "Temp", "Unity", "AMD", "Apple", "Comms", "IGDump",
        "DeviceSearchCache", "Packages", "PlaceholderTileLogoFolder", "Mixed Reality", "BraveSoftware", "Opera Software",
    ];

    /// <summary>Subpastas que não são jogos (acordos, logs, caches, relatórios de erro).</summary>
    static readonly string[] Noise = ["Agreements", "logs", "debuglogs", "Crashes", "CrashReports", "Cache", "webviewdata", "Temp", "ShaderCache"];

    static bool IsNoise(string path)
    {
        var name = Path.GetFileName(path.TrimEnd('\\'));
        return Noise.Contains(name, StringComparer.OrdinalIgnoreCase) || name.StartsWith("com.", StringComparison.OrdinalIgnoreCase)
               || name.Contains("crash", StringComparison.OrdinalIgnoreCase) || name.Contains("webview", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Pastas de estúdios em Documentos que guardam saves (uma subpasta por jogo).</summary>
    static readonly string[] DocStudios = ["Rockstar Games", "Electronic Arts", "BioWare", "Paradox Interactive", "Klei", "EA Games", "Respawn", "Larian Studios"];

    static string Docs => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static List<GameSave> Detect()
    {
        var list = new List<GameSave>();
        void Add(string name, string path, string source, bool cloud)
        {
            if (!Directory.Exists(path) || !Directory.EnumerateFileSystemEntries(path).Any() || IsNoise(path)) return;
            if (list.Any(g => string.Equals(g.Path, path, StringComparison.OrdinalIgnoreCase))) return;
            list.Add(new GameSave("game:" + path.ToLowerInvariant(), name, path, source, cloud));
        }

        foreach (var d in Subdirs(Path.Combine(Docs, "My Games"))) Add(Path.GetFileName(d), d, @"Documentos\My Games", false);
        foreach (var d in Subdirs(Path.Combine(Sys.UserProfile, "Saved Games"))) Add(Path.GetFileName(d), d, "Saved Games", false);
        foreach (var studio in DocStudios)
            foreach (var d in Subdirs(Path.Combine(Docs, studio))) Add(Path.GetFileName(d), d, $@"Documentos\{studio}", false);

        // Unreal Engine: %LOCALAPPDATA%\<Jogo>\Saved\SaveGames
        foreach (var d in Subdirs(Local))
        {
            var saves = Path.Combine(d, "Saved", "SaveGames");
            if (Directory.Exists(saves)) Add(Path.GetFileName(d), saves, "Unreal Engine", false);
        }

        // Unity: AppData\LocalLow\<Estúdio>\<Jogo>
        var localLow = Path.Combine(Sys.UserProfile, "AppData", "LocalLow");
        foreach (var studio in Subdirs(localLow).Where(s => !NotGames.Contains(Path.GetFileName(s), StringComparer.OrdinalIgnoreCase) && !IsNoise(s)))
            foreach (var game in Subdirs(studio)) Add(Path.GetFileName(game), game, "Unity", false);

        // Steam Cloud: <Steam>\userdata\<conta>\<appid>\remote
        var steam = SteamPath();
        if (steam != null)
        {
            var names = SteamAppNames(steam);
            foreach (var account in Subdirs(Path.Combine(steam, "userdata")))
                foreach (var app in Subdirs(account))
                {
                    var id = Path.GetFileName(app);
                    if (id is "7" or "760" or "241100") continue; // cliente, capturas de tela, controles
                    var remote = Path.Combine(app, "remote");
                    Add(names.GetValueOrDefault(id, $"Jogo da Steam {id}"), remote, "Steam Cloud", true);
                }
        }

        // Ubisoft Connect (sincroniza na nuvem da Ubisoft)
        var ubi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Ubisoft\Ubisoft Game Launcher\savegames");
        foreach (var account in Subdirs(ubi))
            foreach (var game in Subdirs(account)) Add($"Ubisoft (jogo {Path.GetFileName(game)})", game, "Ubisoft Connect", true);

        foreach (var custom in Settings.Data.GameCustomFolders)
            if (Directory.Exists(custom)) Add(Path.GetFileName(custom.TrimEnd('\\')), custom, "Adicionado por você", false);

        return list.OrderBy(g => g.Cloud).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static IEnumerable<string> Subdirs(string path)
    {
        try { return Directory.Exists(path) ? Directory.GetDirectories(path) : []; }
        catch (Exception) { return []; }
    }

    public static string? SteamPath()
    {
        var p = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        return p != null && Directory.Exists(p) ? Path.GetFullPath(p.Replace('/', '\\')) : null;
    }

    /// <summary>Bibliotecas da Steam, de steamapps\libraryfolders.vdf.</summary>
    public static List<string> SteamLibraries()
    {
        var steam = SteamPath();
        if (steam == null) return new();
        var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        var list = new List<string> { steam };
        if (File.Exists(vdf))
            foreach (Match m in VdfPath().Matches(File.ReadAllText(vdf)))
                list.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        return list.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).ToList();
    }

    static Dictionary<string, string> SteamAppNames(string steam)
    {
        var names = new Dictionary<string, string>();
        foreach (var lib in SteamLibraries())
            foreach (var acf in Directory.Exists(Path.Combine(lib, "steamapps")) ? Directory.GetFiles(Path.Combine(lib, "steamapps"), "appmanifest_*.acf") : [])
            {
                var text = File.ReadAllText(acf);
                var id = AcfField(text, "appid");
                var name = AcfField(text, "name");
                if (id != null && name != null) names[id] = name;
            }
        return names;
    }

    static string? AcfField(string text, string field)
    {
        var m = Regex.Match(text, $"\"{field}\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex VdfPath();

    // ---------- Seleção ----------

    /// <summary>Marcado por padrão, exceto saves que já estão na nuvem quando "Pular jogos com save na nuvem" está ligado.</summary>
    public static bool IsSelected(GameSave g) =>
        Settings.Data.GamesSelection.TryGetValue(g.Key, out var on) ? on : !(g.Cloud && Settings.Data.SkipCloudGames);

    public static List<FolderSpec> SelectedFolders(List<GameSave>? games = null) =>
        (games ?? Detect()).Where(IsSelected)
            .Select(g => new FolderSpec(g.Key, Path.GetFileName(g.Path.TrimEnd('\\')), g.Path, false, false, Path.Combine("Saves de jogos", Safe(g.Name))))
            .ToList();

    static string Safe(string s) => new(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
}
