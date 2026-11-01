using System.Text.Json;

namespace WINbackup.Backup;

/// <summary>
/// manifest.json de cada versão (WINbackup_AAAA-MM-DD_HH-MM-SS). Lista tudo o que a versão
/// contém; nos backups incrementais, os arquivos sem mudança apontam para a versão onde estão guardados.
/// </summary>
public class Manifest
{
    public const string FileName = "manifest.json";

    public int Format { get; set; } = 1;
    public string App { get; set; } = "WINbackup 0.1 beta";
    public DateTime CreatedAt { get; set; }
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "Completo";
    public string Computer { get; set; } = Environment.MachineName;
    public string User { get; set; } = Environment.UserName;
    /// <summary>Pasta do usuário no momento do backup (para restaurar em outro usuário ou depois de formatar).</summary>
    public string UserProfile { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string? BasedOn { get; set; }
    public bool Completed { get; set; }
    public bool Encrypted { get; set; }
    public List<string> Modules { get; set; } = new();
    public List<ManifestFolder> Folders { get; set; } = new();
    public List<ManifestFile> Files { get; set; } = new();
    public List<ManifestDir> EmptyFolders { get; set; } = new();
    public List<ManifestDriver> Drivers { get; set; } = new();
    public long BytesStored { get; set; }
    public bool HashChecked { get; set; }
    public int HashMismatches { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public void Save(string versionPath) =>
        File.WriteAllText(Path.Combine(versionPath, FileName), JsonSerializer.Serialize(this, Json));

    public static Manifest? Load(string versionPath)
    {
        try
        {
            var file = Path.Combine(versionPath, FileName);
            return File.Exists(file) ? JsonSerializer.Deserialize<Manifest>(File.ReadAllText(file)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

public class ManifestFolder
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    /// <summary>Pasta do backup onde ela está: "Arquivos" ou "Criadores\Photoshop"…</summary>
    public string Group { get; set; } = "Arquivos";
}

public class ManifestFile
{
    public string Folder { get; set; } = "";
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public DateTime Modified { get; set; }
    public string? Sha256 { get; set; }
    /// <summary>Caminho do arquivo guardado, relativo à pasta de destino dos backups.</summary>
    public string Stored { get; set; } = "";
}

public class ManifestDir
{
    public string Folder { get; set; } = "";
    public string Path { get; set; } = "";
}

public class ManifestDriver
{
    public string Inf { get; set; } = "";
    public string Device { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Class { get; set; } = "";
    public string Version { get; set; } = "";
}
