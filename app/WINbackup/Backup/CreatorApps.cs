using WINbackup.Services;

namespace WINbackup.Backup;

/// <summary>
/// Programas de criadores (edição, imagem, lives, áudio, 3D) e onde cada um guarda presets,
/// workspaces, scripts e plugins no Windows. Só aparece o que existir no PC.
/// Tokens: {Docs}, {Pictures}, %APPDATA%, %LOCALAPPDATA%, %PROGRAMDATA%, %ProgramFiles%, %CommonProgramFiles%.
/// Um segmento com * casa com pastas de versão ("Adobe Photoshop *").
/// </summary>
public static class CreatorApps
{
    public record App(string Key, string Category, string Name, string Note, string[] Paths, bool Plugins = false);

    public static readonly string[] Categories = ["Edição de vídeo", "Imagem e design", "Lives", "Áudio", "3D e motion"];

    public static readonly App[] Catalog =
    [
        // Edição de vídeo
        new("premiere", "Edição de vídeo", "Premiere Pro", "Perfis, workspaces, atalhos e presets de efeitos e sequência",
            [@"{Docs}\Adobe\Premiere Pro", @"%APPDATA%\Adobe\Premiere Pro"]),
        new("aftereffects", "Edição de vídeo", "After Effects", "User Presets, preferências, atalhos e scripts (ScriptUI Panels)",
            [@"{Docs}\Adobe\After Effects *", @"%APPDATA%\Adobe\After Effects", @"%ProgramFiles%\Adobe\Adobe After Effects *\Support Files\Scripts"]),
        new("mogrt", "Edição de vídeo", "Motion Graphics Templates (.mogrt)", "Modelos do painel Gráficos essenciais",
            [@"%APPDATA%\Adobe\Common\Motion Graphics Templates", @"{Docs}\Adobe\Common\Motion Graphics Templates"]),
        new("ame", "Edição de vídeo", "Media Encoder", "Presets de exportação",
            [@"{Docs}\Adobe\Adobe Media Encoder"]),
        new("cep", "Edição de vídeo", "Extensões Adobe (painéis CEP)", "Painéis instalados para Premiere, After Effects e Photoshop",
            [@"%APPDATA%\Adobe\CEP\extensions", @"%CommonProgramFiles%\Adobe\CEP\extensions"]),
        new("resolve", "Edição de vídeo", "DaVinci Resolve", "Macros e templates do Fusion, scripts, presets, preferências e banco de projetos local",
            [@"%APPDATA%\Blackmagic Design\DaVinci Resolve", @"%PROGRAMDATA%\Blackmagic Design\DaVinci Resolve\Support\LUT",
             @"%PROGRAMDATA%\Blackmagic Design\DaVinci Resolve\Fusion"]),
        new("aeplugins", "Edição de vídeo", "Plugins do After Effects", "Pasta Plug-ins (Video Copilot, Red Giant, Boris FX…)",
            [@"%ProgramFiles%\Adobe\Adobe After Effects *\Support Files\Plug-ins"], Plugins: true),
        new("mediacore", "Edição de vídeo", "Plugins compartilhados Adobe (MediaCore)", "Plugins usados por Premiere e After Effects",
            [@"%CommonProgramFiles%\Adobe\Plug-ins\7.0\MediaCore"], Plugins: true),
        new("ofx", "Edição de vídeo", "Plugins OFX (DaVinci Resolve)", "Boris FX, Red Giant, Neat Video e outros OpenFX",
            [@"%CommonProgramFiles%\OFX\Plugins"], Plugins: true),

        // Imagem e design
        new("photoshop", "Imagem e design", "Photoshop", "Pincéis, amostras, ações, workspaces e preferências",
            [@"%APPDATA%\Adobe\Adobe Photoshop *"]),
        new("illustrator", "Imagem e design", "Illustrator", "Amostras, pincéis, workspaces e preferências",
            [@"%APPDATA%\Adobe\Adobe Illustrator *"]),
        new("lightroom", "Imagem e design", "Lightroom Classic", "Presets (Camera Raw) e modelos; o catálogo padrão fica em Imagens\\Lightroom",
            [@"%APPDATA%\Adobe\CameraRaw\Settings", @"%APPDATA%\Adobe\Lightroom", @"{Pictures}\Lightroom"]),
        new("captureone", "Imagem e design", "Capture One", "Estilos, presets e preferências",
            [@"%LOCALAPPDATA%\CaptureOne"]),
        new("affinity", "Imagem e design", "Affinity (Photo, Designer, Publisher)", "Pincéis, paletas e preferências",
            [@"%APPDATA%\Affinity"]),
        new("fonts", "Imagem e design", "Fontes instaladas por você", "Fontes do usuário (não inclui as do Windows)",
            [@"%LOCALAPPDATA%\Microsoft\Windows\Fonts"]),
        new("psplugins", "Imagem e design", "Plugins do Photoshop", "Pasta Plug-ins do Photoshop e plugins Adobe compartilhados",
            [@"%ProgramFiles%\Adobe\Adobe Photoshop *\Plug-ins", @"%CommonProgramFiles%\Adobe\Plug-Ins\CC"], Plugins: true),

        // Lives
        new("obs", "Lives", "OBS Studio", "Cenas, perfis, coleções e configuração dos plugins",
            [@"%APPDATA%\obs-studio"]),
        new("obsplugins", "Lives", "Plugins do OBS", "Plugins instalados no OBS",
            [@"%ProgramFiles%\obs-studio\obs-plugins", @"%PROGRAMDATA%\obs-studio\plugins"], Plugins: true),
        new("streamdeck", "Lives", "Stream Deck", "Perfis, ícones e plugins",
            [@"%APPDATA%\Elgato\StreamDeck"]),
        new("elgato", "Lives", "Elgato Camera Hub e Wave Link", "Ajustes de câmera e mixagem de áudio",
            [@"%APPDATA%\Elgato\CameraHub", @"%APPDATA%\Elgato\WaveLink"]),
        new("streamerbot", "Lives", "Streamer.bot", "Ações, comandos e configurações (pasta portátil)",
            [@"{Docs}\Streamer.bot", @"%USERPROFILE%\Desktop\Streamer.bot", @"C:\Streamer.bot", @"%LOCALAPPDATA%\Streamer.bot"]),
        new("mixitup", "Lives", "Mix It Up", "Comandos e configurações do bot",
            [@"%LOCALAPPDATA%\MixItUp"]),
        new("touchportal", "Lives", "Touch Portal", "Páginas, botões e plugins",
            [@"%APPDATA%\TouchPortal"]),

        // Áudio
        new("voicemeeter", "Áudio", "Voicemeeter", "Configuração salva (VoicemeeterDefault.xml)",
            [@"%APPDATA%\VoiceMeeter"]),
        new("broadcast", "Áudio", "NVIDIA Broadcast", "Efeitos de microfone e câmera",
            [@"%LOCALAPPDATA%\NVIDIA Corporation\NVIDIA Broadcast", @"%PROGRAMDATA%\NVIDIA Corporation\NVIDIA Broadcast"]),
        new("audition", "Áudio", "Audition", "Presets de efeitos e workspaces",
            [@"%APPDATA%\Adobe\Audition"]),
        new("reaper", "Áudio", "Reaper", "Configuração, temas, scripts e presets",
            [@"%APPDATA%\REAPER"]),
        new("vstpresets", "Áudio", "Presets de plugins de áudio", "VST3 Presets, iZotope, FabFilter, Image-Line",
            [@"{Docs}\VST3 Presets", @"{Docs}\iZotope", @"{Docs}\FabFilter", @"{Docs}\Image-Line"]),
        new("vst", "Áudio", "Plugins VST", "VST3 e VST2 instalados",
            [@"%CommonProgramFiles%\VST3", @"%ProgramFiles%\VSTPlugins", @"%ProgramFiles%\Steinberg\VSTPlugins"], Plugins: true),

        // 3D e motion
        new("blender", "3D e motion", "Blender", "Preferências, add-ons e presets de todas as versões",
            [@"%APPDATA%\Blender Foundation\Blender"]),
        new("c4d", "3D e motion", "Cinema 4D e Red Giant (Maxon)", "Preferências, layouts e presets",
            [@"%APPDATA%\Maxon"]),
        new("element3d", "3D e motion", "Element 3D (Video Copilot)", "Modelos e materiais",
            [@"{Docs}\VideoCopilot"]),
    ];

    /// <summary>Lixo que não vale copiar: logs, caches de mídia e prévias.</summary>
    public static readonly string[] Exclusions =
    [
        "logs", "*.log", "CacheClip", "Media Cache", "Media Cache Files", "Peak Files", "Crashpad", "crashes",
        "*Previews.lrdata", "*Smart Previews.lrdata", "GPUCache", "Code Cache", "ProxyMedia",
        "*Audio Previews", "*Video Previews", "*.PRV", "*.cfa", "*.pek",
    ];

    public static List<string> Resolve(App app) => app.Paths.SelectMany(Expand).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    static IEnumerable<string> Expand(string pattern)
    {
        var p = pattern.Replace("{Docs}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))
                       .Replace("{Pictures}", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        p = Environment.ExpandEnvironmentVariables(p);
        if (!p.Contains('*')) return [p];

        // Expande um segmento com * por vez (pastas de versão)
        IEnumerable<string> results = [Path.GetPathRoot(p)!];
        foreach (var part in p[Path.GetPathRoot(p)!.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            results = results.SelectMany(parent =>
            {
                if (!part.Contains('*')) return [Path.Combine(parent, part)];
                try { return Directory.Exists(parent) ? Directory.GetDirectories(parent, part) : []; }
                catch (Exception) { return []; }
            }).ToList();
        }
        return results;
    }

    public static bool IsSelected(App app) =>
        Settings.Data.CreatorSelection.TryGetValue(app.Key, out var on) ? on : !app.Plugins || Settings.Data.CreatorPlugins;

    /// <summary>Pastas a copiar, agrupadas em Criadores\&lt;Programa&gt; no backup.</summary>
    public static List<FolderSpec> SelectedFolders()
    {
        var list = new List<FolderSpec>();
        foreach (var app in Catalog.Where(IsSelected))
        {
            if (app.Plugins && !Settings.Data.CreatorPlugins) continue;
            foreach (var path in Resolve(app))
                list.Add(new FolderSpec("creator:" + path.ToLowerInvariant(), Label(path), path, false, false, Path.Combine("Criadores", Safe(app.Name))));
        }
        foreach (var path in Settings.Data.CreatorCustomFolders.Where(Directory.Exists))
            list.Add(new FolderSpec("creator:" + path.ToLowerInvariant(), Label(path), path, false, true, Path.Combine("Criadores", "Pastas adicionadas")));
        return list;
    }

    /// <summary>Nome legível da pasta no backup: "Roaming - Adobe - Adobe Photoshop 2025".</summary>
    static string Label(string path)
    {
        var parts = path.TrimEnd('\\').Split('\\');
        return Safe(string.Join(" - ", parts.Skip(Math.Max(1, parts.Length - 3))));
    }

    static string Safe(string s) => new(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
}
