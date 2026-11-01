using WINbackup.Services;

namespace WINbackup.Backup;

public enum JobKind { Backup, RestoreDrivers, Restore }

/// <summary>Group = pasta do backup onde ela fica ("Arquivos", "Criadores\Photoshop"…).</summary>
public record FolderSpec(string Key, string Name, string Path, bool Sensitive, bool Custom, string Group = "Arquivos");

/// <summary>O que uma execução vai fazer. Montado pelas telas a partir das escolhas do usuário.</summary>
public class JobSpec
{
    public JobKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Destination { get; init; } = Settings.Data.Destination;

    public List<FolderSpec> Folders { get; init; } = new();
    /// <summary>Nome da etapa de cópia de pastas no progresso e no relatório.</summary>
    public string FilesLabel { get; init; } = "Arquivos pessoais";
    public bool Drivers { get; init; }
    /// <summary>Pacotes oemNN.inf escolhidos; null = todos os de terceiros.</summary>
    public List<string>? DriverInfs { get; init; }
    /// <summary>Módulos do preset que ainda não existem nesta versão (aparecem como "Em breve").</summary>
    public List<string> Unavailable { get; init; } = new();

    public string? DriverSource { get; init; }

    // Restauração
    public List<RestoreSource> RestoreSources { get; init; } = new();
    public RestoreTarget Target { get; init; }
    public string? TargetPath { get; init; }
    public ConflictMode Conflict { get; init; }
    /// <summary>Instala os drivers sem pedir para reiniciar no fim (padrão: pede).</summary>
    public bool NoReboot { get; init; }

    public bool IsRestore => Kind != JobKind.Backup;

    public bool VerifyHash { get; init; } = Settings.Data.VerifyHash;
    public bool UseShadowCopy { get; init; } = Settings.Data.UseShadowCopy;
    public bool Incremental { get; init; } = Settings.Data.Incremental;
    public bool KeepAcl { get; init; } = Settings.Data.KeepAcl;
    public bool DownloadCloudFiles { get; init; } = Settings.Data.DownloadCloudFiles;
    public List<string> Exclusions { get; init; } = new(Settings.Data.Exclusions);

    public bool HasWork => Folders.Count > 0 || Drivers;
}

/// <summary>Pastas pessoais padrão + pastas adicionadas pelo usuário.</summary>
public static class PersonalFolders
{
    public static List<FolderSpec> All()
    {
        var list = new List<FolderSpec>
        {
            new("doc", "Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), false, false),
            new("img", "Imagens", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), false, false),
            new("mus", "Músicas", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), false, false),
            new("vid", "Vídeos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), false, false),
            new("dwn", "Downloads", Sys.Downloads, false, false),
            new("dsk", "Área de Trabalho", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), false, false),
            new("ssh", ".ssh", Path.Combine(Sys.UserProfile, ".ssh"), true, false),
        };
        foreach (var p in Settings.Data.CustomFolders)
            list.Add(new("custom:" + p.ToLowerInvariant(), Path.GetFileName(p.TrimEnd('\\')) is { Length: > 0 } n ? n : p, p, false, true));
        return list;
    }

    public static bool IsSelected(FolderSpec f) =>
        Settings.Data.FilesMode == "C" ? !f.Sensitive : Settings.Data.FolderSelection.GetValueOrDefault(f.Key, !f.Sensitive);

    public static List<FolderSpec> Selected() => All().Where(IsSelected).ToList();

    public static List<FolderSpec> ByKeys(params string[] keys) => All().Where(f => keys.Contains(f.Key)).ToList();
}

/// <summary>Presets do Início. Só Arquivos pessoais e Drivers já executam nesta versão.</summary>
public static class Presets
{
    public record Preset(string Name, string Description, string Slug, string Icon);

    public static readonly Preset[] All =
    [
        new("Backup completo", "Arquivos, programas, configs, jogos, drivers e navegadores", "completo", Controls.Icons.Shield),
        new("Só documentos", "Documentos, Imagens e Área de Trabalho", "documentos", Controls.Icons.Doc),
        new("Ambiente de dev", "Programas, .ssh, Git, VS Code e Terminal", "dev", Controls.Icons.Code),
        new("Gamer", "Saves de jogos, Steam, Discord e OBS", "gamer", Controls.Icons.Games),
        new("Criadores", "Adobe, DaVinci, OBS, Stream Deck, áudio e 3D", "criadores", Controls.Icons.Creator),
    ];

    public static JobSpec Creators() => new()
    {
        Title = "Criadores", FilesLabel = "Criadores", Folders = CreatorApps.SelectedFolders(),
        Exclusions = Settings.Data.Exclusions.Concat(CreatorApps.Exclusions).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
    };

    public static JobSpec Build(int index)
    {
        var p = All[index];
        return index switch
        {
            0 => new JobSpec
            {
                Title = p.Name, Folders = PersonalFolders.Selected(), Drivers = true,
                Unavailable = ["Programas (winget)", "Configurações", "Saves de jogos", "Navegadores"],
            },
            1 => new JobSpec { Title = p.Name, Folders = PersonalFolders.ByKeys("doc", "img", "dsk") },
            2 => new JobSpec
            {
                Title = p.Name, Folders = PersonalFolders.ByKeys("ssh").Where(f => Directory.Exists(f.Path)).ToList(),
                Unavailable = ["Programas (winget)", "Configurações"],
            },
            3 => new JobSpec { Title = p.Name, Unavailable = ["Saves de jogos", "Configurações"] },
            _ => Creators(),
        };
    }
}
