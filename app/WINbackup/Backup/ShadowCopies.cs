using System.Management;

namespace WINbackup.Backup;

/// <summary>
/// Cópias de sombra (VSS) criadas sob demanda, uma por volume, só quando aparece
/// um arquivo travado. São apagadas ao fim do backup.
/// </summary>
public sealed class ShadowCopies : IDisposable
{
    readonly BackupRunner _runner;
    readonly Dictionary<string, (string Id, string Device)?> _byVolume = new(StringComparer.OrdinalIgnoreCase);

    public int Used { get; private set; }
    public int Created => _byVolume.Values.Count(v => v != null);
    public int Failed => _byVolume.Values.Count(v => v == null);

    public ShadowCopies(BackupRunner runner) => _runner = runner;

    /// <summary>Caminho do mesmo arquivo dentro da cópia de sombra, ou null se não deu para criar.</summary>
    public string? Map(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\")) return null;
        if (!_byVolume.TryGetValue(root, out var shadow))
        {
            shadow = Create(root);
            _byVolume[root] = shadow;
        }
        if (shadow == null) return null;
        Used++;
        return shadow.Value.Device + path[(root.Length - 1)..];
    }

    (string, string)? Create(string volume)
    {
        try
        {
            _runner.Info($"Criando Shadow Copy (VSS) de {volume.TrimEnd('\\')}");
            using var cls = new ManagementClass(@"\\.\root\cimv2", "Win32_ShadowCopy", null);
            using var inParams = cls.GetMethodParameters("Create");
            inParams["Volume"] = volume;
            inParams["Context"] = "ClientAccessible";
            using var result = cls.InvokeMethod("Create", inParams, null);
            var code = Convert.ToUInt32(result["ReturnValue"]);
            if (code != 0)
            {
                _runner.Warn($"Não foi possível criar a Shadow Copy de {volume.TrimEnd('\\')} (código {code})");
                return null;
            }
            var id = (string)result["ShadowID"];
            using var sc = new ManagementObject($@"\\.\root\cimv2:Win32_ShadowCopy.ID='{id}'");
            sc.Get();
            var device = (string)sc["DeviceObject"];
            _runner.Info($"Shadow Copy criada para {volume.TrimEnd('\\')}");
            return (id, device);
        }
        catch (Exception ex)
        {
            _runner.Warn($"Shadow Copy indisponível em {volume.TrimEnd('\\')}: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var shadow in _byVolume.Values)
        {
            if (shadow == null) continue;
            try
            {
                using var sc = new ManagementObject($@"\\.\root\cimv2:Win32_ShadowCopy.ID='{shadow.Value.Id}'");
                sc.Delete();
            }
            catch (Exception ex)
            {
                _runner.Warn($"Não foi possível apagar a Shadow Copy {shadow.Value.Id}: {ex.Message}", report: false);
            }
        }
        _byVolume.Clear();
    }
}
