using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace WINbackup.Services;

public static partial class Sys
{
    public static bool IsAdmin { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// Abre um link ou pasta pelo Explorer. Como o app roda como administrador,
    /// isso evita abrir o navegador também elevado.
    /// </summary>
    public static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{target}\"") { UseShellExecute = false }); }
        catch (Exception ex) { CrashLog.Write(ex); }
    }

    public static string Downloads => KnownFolder(new Guid("374DE290-123F-4565-9164-39C4925E467B")) ??
                                      Path.Combine(UserProfile, "Downloads");

    public static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Raiz do OneDrive, quando o usuário tem o OneDrive configurado.</summary>
    public static string? OneDriveRoot
    {
        get
        {
            foreach (var v in new[] { L.T("OneDrive"), L.T("OneDriveConsumer"), L.T("OneDriveCommercial") })
            {
                var p = Environment.GetEnvironmentVariable(v);
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p.TrimEnd('\\');
            }
            return null;
        }
    }

    public static bool IsInOneDrive(string path)
    {
        var root = OneDriveRoot;
        return root != null && path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Caminho curto para exibir: %OneDrive%\… e %USERPROFILE%\…, como no protótipo.</summary>
    public static string Pretty(string path)
    {
        var od = OneDriveRoot;
        if (od != null && path.StartsWith(od, StringComparison.OrdinalIgnoreCase)) return "%OneDrive%" + path[od.Length..];
        var up = UserProfile;
        if (path.StartsWith(up, StringComparison.OrdinalIgnoreCase)) return "%USERPROFILE%" + path[up.Length..];
        return path;
    }

    public static string Bytes(long b)
    {
        string[] u = ["B", "KB", "MB", "GB", "TB"];
        double v = b;
        int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{b} B" : $"{v:0.#} {u[i]}";
    }

    public static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";

    static string? KnownFolder(Guid id)
    {
        if (SHGetKnownFolderPath(id, 0, IntPtr.Zero, out var p) != 0) return null;
        try { return Marshal.PtrToStringUni(p); }
        finally { Marshal.FreeCoTaskMem(p); }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid rfid, uint flags, IntPtr token, out IntPtr path);
}

public static class CrashLog
{
    public static void Write(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Settings.Folder);
            File.AppendAllText(Path.Combine(Settings.Folder, "erros.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch { }
    }
}
