using System.Runtime.InteropServices;
using WINbackup.Controls;

namespace WINbackup.Services;

public record DestOption(string Label, string Path, string Icon);

/// <summary>Discos onde dá para salvar os backups e o espaço livre de cada um.</summary>
public static partial class Destinations
{
    public static List<DestOption> Candidates()
    {
        var system = System.IO.Path.GetPathRoot(Environment.SystemDirectory);
        var list = new List<DestOption>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || string.Equals(d.Name, system, StringComparison.OrdinalIgnoreCase)) continue;
                if (d.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network)) continue;
                list.Add(new DestOption(LabelFor(d), System.IO.Path.Combine(d.Name, "Backup"),
                    d.DriveType == DriveType.Network ? Icons.Network : Icons.Drive));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return list;
    }

    static string LabelFor(DriveInfo d)
    {
        var letter = d.Name.TrimEnd('\\');
        if (!string.IsNullOrWhiteSpace(d.VolumeLabel)) return $"{d.VolumeLabel} ({letter})";
        return d.DriveType switch
        {
            DriveType.Removable => $"Disco removível ({letter})",
            DriveType.Network => $"Unidade de rede ({letter})",
            _ => $"Disco local ({letter})",
        };
    }

    /// <summary>Nome amigável para qualquer caminho (inclusive pastas escolhidas à mão).</summary>
    public static DestOption Describe(string path)
    {
        if (string.IsNullOrEmpty(path)) return new DestOption("Nenhum destino", "Escolha onde salvar", Icons.Drive);
        var match = Candidates().FirstOrDefault(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;
        if (path.StartsWith(@"\\")) return new DestOption("Pasta na rede", path, Icons.Network);
        try
        {
            var root = System.IO.Path.GetPathRoot(path);
            if (root != null)
            {
                var d = new DriveInfo(root);
                if (d.IsReady) return new DestOption(LabelFor(d), path, Icons.Drive);
            }
        }
        catch (ArgumentException) { }
        catch (IOException) { }
        return new DestOption("Pasta escolhida", path, Icons.Drive);
    }

    /// <summary>Espaço livre e total do volume do caminho (funciona com caminhos de rede).</summary>
    public static (long Free, long Total)? Space(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var probe = path;
        while (!string.IsNullOrEmpty(probe) && !Directory.Exists(probe))
            probe = System.IO.Path.GetDirectoryName(probe);
        if (string.IsNullOrEmpty(probe)) probe = System.IO.Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(probe)) return null;
        if (!GetDiskFreeSpaceEx(probe, out var free, out var total, out _)) return null;
        return ((long)free, (long)total);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetDiskFreeSpaceEx(string dir, out ulong freeForCaller, out ulong total, out ulong totalFree);

    /// <summary>Pastas de versão (WINbackup_*) no destino atual, da mais nova para a mais antiga.</summary>
    public static List<string> Versions(string? dest = null)
    {
        dest ??= Settings.Data.Destination;
        try
        {
            if (string.IsNullOrEmpty(dest) || !Directory.Exists(dest)) return new();
            return Directory.GetDirectories(dest, "WINbackup_*").OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception)
        {
            return new();
        }
    }
}
