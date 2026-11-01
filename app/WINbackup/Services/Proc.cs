using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WINbackup.Services;

public static class Proc
{
    /// <summary>Roda um comando do sistema e devolve o código de saída, repassando cada linha da saída.</summary>
    public static async Task<int> RunAsync(string exe, string args, Action<string>? onLine, CancellationToken token, Encoding? encoding = null)
    {
        var oem = encoding ?? Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = oem,
            StandardErrorEncoding = oem,
        };
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) onLine?.Invoke(e.Data.Trim()); };
        p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) onLine?.Invoke(e.Data.Trim()); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        try
        {
            await p.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            throw;
        }
        return p.ExitCode;
    }

    public static string SystemExe(string name) => Path.Combine(Environment.SystemDirectory, name);

    /// <summary>winget.exe (alias do Instalador de Aplicativo), ou null se não estiver instalado.</summary>
    public static string? Winget()
    {
        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(alias)) return alias;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "winget.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
