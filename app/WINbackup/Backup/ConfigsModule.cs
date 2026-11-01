using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Backup;

/// <summary>
/// Configurações do Windows (Wi-Fi, tema, impressoras, fontes, variáveis, hosts) e de apps
/// (Terminal, PowerShell, VS Code, Git…). As do Windows são exportadas com as ferramentas do
/// próprio sistema para Configuracoes\Windows; as de apps viram pastas comuns do backup.
/// </summary>
public static partial class ConfigsModule
{
    public record WinItem(string Key, string Name, string Description);

    public static readonly WinItem[] WindowsItems =
    [
        new("wifi", "Redes Wi-Fi", "Perfis salvos, com senha"),
        new("theme", "Papel de parede e tema", "Fundo, cor de destaque, modo escuro"),
        new("print", "Impressoras", "Filas e drivers de impressão"),
        new("fonts", "Fontes instaladas por você", "Fontes do usuário (as do Windows ficam de fora)"),
        new("env", "Variáveis de ambiente", "Do usuário, incluindo o PATH personalizado"),
        new("hosts", "Arquivo hosts", @"C:\Windows\System32\drivers\etc\hosts"),
    ];

    public record AppDef(string Key, string Name, string Note, string[] Paths, string[]? Include = null);

    /// <summary>Configurações de apps. OBS, Streamer.bot e afins ficam em Criadores.</summary>
    public static readonly AppDef[] Apps =
    [
        new("terminal", "Windows Terminal", "settings.json, perfis e temas",
            [@"%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState", @"%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState"],
            ["settings.json", "state.json"]),
        new("pwsh", "Perfis do PowerShell", "$PROFILE do PowerShell 7 e do Windows PowerShell",
            [@"{Docs}\PowerShell", @"{Docs}\WindowsPowerShell"],
            ["Microsoft.PowerShell_profile.ps1", "profile.ps1", "Microsoft.VSCode_profile.ps1"]),
        new("vscode", "VS Code", "Configurações, atalhos, snippets e a lista de extensões",
            [@"%APPDATA%\Code\User", @"%APPDATA%\Cursor\User"], ["settings.json", "keybindings.json", "snippets", "tasks.json", "profiles"]),
        new("git", "Git", ".gitconfig e .gitignore global", [@"%USERPROFILE%"], [".gitconfig", ".gitignore_global", ".gitattributes"]),
        new("notepadpp", "Notepad++", "Configurações, atalhos e temas", [@"%APPDATA%\Notepad++"]),
        new("powertoys", "PowerToys", "Configurações de todos os utilitários", [@"%LOCALAPPDATA%\Microsoft\PowerToys"]),
        new("komorebi", "Komorebi", "komorebi.json, applications.yaml e whkdrc",
            [@"%USERPROFILE%", @"%USERPROFILE%\.config"], ["komorebi.json", "komorebi.bar.json", "applications.yaml", "applications.json", "whkdrc"]),
        new("yasb", "YASB", "Barra e estilos", [@"%USERPROFILE%\.config\yasb"]),
        new("ahk", "AutoHotkey", "Seus scripts", [@"{Docs}\AutoHotkey"]),
        new("rainmeter", "Rainmeter", "Skins e layouts", [@"{Docs}\Rainmeter", @"%APPDATA%\Rainmeter"]),
    ];

    static readonly string[] AppExclusions = ["logs", "*.log", "RunnerLogs", "Cache", "Crashpad", "CachedData", "History", "workspaceStorage", "globalStorage", "*.cli-lock", "*.lock"];

    static string Expand(string p) => Environment.ExpandEnvironmentVariables(p.Replace("{Docs}", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));

    /// <summary>Pastas existentes do app (com os itens de Include, quando houver, presentes nelas).</summary>
    public static List<string> Resolve(AppDef app) => app.Paths.Select(Expand).Where(Directory.Exists)
        .Where(p => app.Include == null || app.Include.Any(i => File.Exists(Path.Combine(p, i)) || Directory.Exists(Path.Combine(p, i))))
        .ToList();

    public static bool IsSelected(string key) => !Settings.Data.ConfigsOff.Contains(key, StringComparer.OrdinalIgnoreCase);

    public static List<FolderSpec> SelectedAppFolders()
    {
        var list = new List<FolderSpec>();
        foreach (var app in Apps.Where(a => IsSelected(a.Key)))
            foreach (var path in Resolve(app))
                list.Add(new FolderSpec("config:" + app.Key + ":" + path.ToLowerInvariant(), Path.GetFileName(path.TrimEnd('\\')), path, false, false,
                    Path.Combine("Configurações", app.Name), app.Include));
        if (IsSelected("fonts") && Directory.Exists(UserFonts))
            list.Add(new FolderSpec("config:fonts", "Fonts", UserFonts, false, false, Path.Combine("Configurações", "Fontes")));
        foreach (var path in Settings.Data.ConfigCustomFolders.Where(Directory.Exists))
            list.Add(new FolderSpec("config:custom:" + path.ToLowerInvariant(), Path.GetFileName(path.TrimEnd('\\')), path, false, true,
                Path.Combine("Configurações", "Pastas adicionadas")));
        return list;
    }

    public static List<string> Exclusions => Settings.Data.Exclusions.Concat(AppExclusions).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    static string UserFonts => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");
    static string Hosts => Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts");

    // ---------- Contagens para a tela ----------

    public static async Task<List<string>> WifiProfilesAsync(CancellationToken token)
    {
        var lines = new List<string>();
        await Proc.RunAsync(Proc.SystemExe("netsh.exe"), "wlan show profiles", lines.Add, token);
        // Os perfis vêm depois do último separador "-----", um por linha: "<rótulo traduzido>: <nome>"
        var last = lines.FindLastIndex(l => l.Length > 3 && l.All(c => c == '-'));
        return lines.Skip(last + 1).Where(l => l.Contains(':')).Select(l => l[(l.IndexOf(':') + 1)..].Trim()).Where(n => n.Length > 0).ToList();
    }

    public static List<string> Printers()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_Printer");
            return s.Get().Cast<ManagementObject>().Select(m => (string)m["Name"]).ToList();
        }
        catch (Exception) { return new(); }
    }

    public static int UserFontCount() => Directory.Exists(UserFonts) ? Directory.GetFiles(UserFonts).Length : 0;

    // ---------- Backup ----------

    /// <summary>Exporta as configurações do Windows para Configuracoes\Windows (as de apps vão pelo motor de cópia).</summary>
    public static async Task BackupWindowsAsync(BackupRunner r, StepItem step, Manifest m, CancellationToken token)
    {
        r.Activity = "Configurações";
        var dir = Path.Combine(r.VersionPath, "Configuracoes", "Windows");
        Directory.CreateDirectory(dir);
        var keys = r.Spec.WindowsKeys;
        var done = new List<string>();
        for (var i = 0; i < keys.Count; i++)
        {
            r.Checkpoint(token);
            var key = keys[i];
            r.CurrentFile = WindowsItems.First(w => w.Key == key).Name;
            r.Step(step, StepState.Running, $"Exportando {r.CurrentFile}…");
            try
            {
                switch (key)
                {
                    case "wifi":
                        var wifi = Directory.CreateDirectory(Path.Combine(dir, "Wi-Fi")).FullName;
                        await Proc.RunAsync(Proc.SystemExe("netsh.exe"), $"wlan export profile key=clear folder=\"{wifi}\"", null, token);
                        var count = Directory.GetFiles(wifi, "*.xml").Length;
                        if (count == 0 && !Sys.IsAdmin)
                        {
                            // Sem administrador o netsh recusa exportar com senha: salva as redes sem a senha
                            await Proc.RunAsync(Proc.SystemExe("netsh.exe"), $"wlan export profile folder=\"{wifi}\"", null, token);
                            count = Directory.GetFiles(wifi, "*.xml").Length;
                            if (count > 0) r.Warn("Sem administrador, as redes Wi-Fi foram salvas sem as senhas.");
                        }
                        if (count == 0) { Directory.Delete(wifi); r.Info("Nenhuma rede Wi-Fi salva"); continue; }
                        r.Ok($"{count} redes Wi-Fi exportadas");
                        break;
                    case "theme":
                        await RegExport(@"HKCU\Control Panel\Desktop", Path.Combine(dir, "tema_desktop.reg"), token);
                        await RegExport(@"HKCU\Control Panel\Colors", Path.Combine(dir, "tema_cores.reg"), token);
                        await RegExport(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", Path.Combine(dir, "tema_personalizar.reg"), token);
                        await RegExport(@"HKCU\Software\Microsoft\Windows\DWM", Path.Combine(dir, "tema_dwm.reg"), token);
                        await RegExport(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", Path.Combine(dir, "tema_destaque.reg"), token);
                        CopyWallpaper(dir);
                        break;
                    case "print":
                        var brm = Path.Combine(Environment.SystemDirectory, @"spool\tools\PrintBrm.exe");
                        if (!File.Exists(brm)) { r.Warn("PrintBrm não existe nesta edição do Windows: impressoras não exportadas."); continue; }
                        var export = Path.Combine(dir, "impressoras.printerExport");
                        var code = await Proc.RunAsync(brm, $"-b -f \"{export}\" -O FORCE", null, token);
                        // Sem administrador o PrintBrm pode sair com 0 sem gerar nada
                        if (code != 0 || !File.Exists(export)) { r.Warn("Impressoras não exportadas: o PrintBrm precisa de administrador."); continue; }
                        break;
                    case "fonts":
                        await RegExport(@"HKCU\Software\Microsoft\Windows NT\CurrentVersion\Fonts", Path.Combine(dir, "fontes.reg"), token);
                        break;
                    case "env":
                        await RegExport(@"HKCU\Environment", Path.Combine(dir, "variaveis.reg"), token);
                        break;
                    case "hosts":
                        File.Copy(Hosts, Path.Combine(dir, "hosts"), true);
                        break;
                }
                done.Add(key);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                r.Warn($"{r.CurrentFile}: {ex.Message}");
            }
            r.Progress(step, (i + 1.0) / Math.Max(1, keys.Count) * 0.8);
        }

        if (IsSelected("vscode") && r.Spec.Folders.Any(f => f.Key.StartsWith("config:vscode"))) await ExportVsCodeExtensions(Path.Combine(r.VersionPath, "Configuracoes"), r, token);
        m.WindowsSettings = done;
        r.StoredBytes += new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        var names = done.Select(k => WindowsItems.First(w => w.Key == k).Name);
        r.Step(step, StepState.Done, done.Count == 0 ? "Nada exportado" : $"{done.Count} itens do Windows");
        r.Modules.Add(new ModuleResult("Configurações do Windows", done.Count == 0 ? "Nada exportado" : string.Join(", ", names), StepState.Done));
    }

    static Task<int> RegExport(string key, string file, CancellationToken token) =>
        Proc.RunAsync(Proc.SystemExe("reg.exe"), $"export \"{key}\" \"{file}\" /y", null, token);

    /// <summary>A imagem atual do fundo: o arquivo original (se existir) ou a cópia que o Windows guarda.</summary>
    static void CopyWallpaper(string dir)
    {
        var target = Path.Combine(dir, "Papel de parede");
        var current = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", null) as string;
        var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
        var source = !string.IsNullOrEmpty(current) && File.Exists(current) ? current : File.Exists(transcoded) ? transcoded : null;
        if (source == null) return;
        Directory.CreateDirectory(target);
        var name = source == transcoded ? "papel-de-parede.jpg" : Path.GetFileName(source);
        File.Copy(source, Path.Combine(target, name), true);
    }

    static string? CodeCli()
    {
        foreach (var p in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\bin\code.cmd"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft VS Code\bin\code.cmd"),
                 })
            if (File.Exists(p)) return p;
        return null;
    }

    static async Task ExportVsCodeExtensions(string dir, BackupRunner r, CancellationToken token)
    {
        var code = CodeCli();
        if (code == null) return;
        var lines = new List<string>();
        await Proc.RunAsync(Proc.SystemExe("cmd.exe"), $"/c \"\"{code}\" --list-extensions\"", lines.Add, token, Encoding.UTF8);
        var exts = lines.Where(l => l.Contains('.') && !l.Contains(' ')).ToList();
        if (exts.Count == 0) return;
        File.WriteAllLines(Path.Combine(dir, "vscode-extensoes.txt"), exts);
        r.Ok($"{exts.Count} extensões do VS Code listadas");
    }

    // ---------- Restauração ----------

    /// <summary>Reaplica as configurações do Windows salvas em Configuracoes\Windows.</summary>
    public static async Task RestoreWindowsAsync(BackupRunner r, StepItem step, string versionPath, Manifest m, CancellationToken token)
    {
        r.Activity = "Configurações";
        var dir = Path.Combine(versionPath, "Configuracoes", "Windows");
        var applied = new List<string>();
        var keys = m.WindowsSettings;
        for (var i = 0; i < keys.Count; i++)
        {
            r.Checkpoint(token);
            var key = keys[i];
            var name = WindowsItems.FirstOrDefault(w => w.Key == key)?.Name ?? key;
            r.CurrentFile = name;
            r.Step(step, StepState.Running, $"Aplicando {name}…");
            try
            {
                switch (key)
                {
                    case "wifi":
                        var wifi = Path.Combine(dir, "Wi-Fi");
                        if (!Directory.Exists(wifi)) continue;
                        var ok = 0;
                        foreach (var xml in Directory.GetFiles(wifi, "*.xml"))
                            if (await Proc.RunAsync(Proc.SystemExe("netsh.exe"), $"wlan add profile filename=\"{xml}\" user=all", null, token) == 0) ok++;
                        r.Ok($"{ok} redes Wi-Fi adicionadas");
                        break;
                    case "theme":
                        foreach (var reg in Directory.GetFiles(dir, "tema_*.reg")) await RegImport(reg, m.UserProfile, token);
                        RestoreWallpaper(dir);
                        break;
                    case "print":
                        var brm = Path.Combine(Environment.SystemDirectory, @"spool\tools\PrintBrm.exe");
                        var file = Path.Combine(dir, "impressoras.printerExport");
                        if (File.Exists(brm) && File.Exists(file))
                            await Proc.RunAsync(brm, $"-r -f \"{file}\" -O FORCE", null, token);
                        break;
                    case "fonts":
                        var fonts = Path.Combine(dir, "fontes.reg");
                        if (File.Exists(fonts)) await RegImport(fonts, m.UserProfile, token);
                        break;
                    case "env":
                        var env = Path.Combine(dir, "variaveis.reg");
                        if (File.Exists(env)) await RegImport(env, m.UserProfile, token);
                        BroadcastSettingChange("Environment");
                        break;
                    case "hosts":
                        var saved = Path.Combine(dir, "hosts");
                        if (!File.Exists(saved)) continue;
                        if (File.Exists(Hosts) && File.ReadAllText(Hosts) != File.ReadAllText(saved))
                            File.Copy(Hosts, Hosts + ".antes-do-winbackup", true);
                        File.Copy(saved, Hosts, true);
                        break;
                }
                applied.Add(name);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                r.Warn($"{name}: {ex.Message}");
            }
            r.Progress(step, (i + 1.0) / Math.Max(1, keys.Count) * 0.7);
        }

        // Extensões do VS Code, depois dos programas
        var list = Path.Combine(versionPath, "Configuracoes", "vscode-extensoes.txt");
        if (File.Exists(list) && CodeCli() is { } code)
        {
            var exts = File.ReadAllLines(list).Where(l => l.Length > 0).ToList();
            for (var i = 0; i < exts.Count; i++)
            {
                r.Checkpoint(token);
                r.CurrentFile = exts[i];
                await Proc.RunAsync(Proc.SystemExe("cmd.exe"), $"/c \"\"{code}\" --install-extension {exts[i]}\"", null, token, Encoding.UTF8);
            }
            applied.Add($"{exts.Count} extensões do VS Code");
        }

        r.Step(step, StepState.Done, applied.Count == 0 ? "Nada a aplicar" : $"{applied.Count} itens aplicados");
        if (keys.Contains("theme") || keys.Contains("fonts"))
            r.Warn("Saia e entre de novo no Windows para o tema e as fontes aparecerem por completo.", report: true);
        r.Modules.Add(new ModuleResult("Configurações do Windows", applied.Count == 0 ? "Nada a aplicar" : string.Join(", ", applied), StepState.Done));
    }

    /// <summary>
    /// reg import, trocando a pasta do usuário antigo pela atual (o nome de usuário pode mudar depois de formatar).
    /// Os .reg do reg.exe são UTF-16 com barras duplicadas.
    /// </summary>
    static async Task RegImport(string file, string oldProfile, CancellationToken token)
    {
        var text = File.ReadAllText(file);
        var current = Sys.UserProfile;
        if (!string.IsNullOrEmpty(oldProfile) && !string.Equals(oldProfile, current, StringComparison.OrdinalIgnoreCase))
            text = text.Replace(oldProfile.Replace("\\", "\\\\"), current.Replace("\\", "\\\\"), StringComparison.OrdinalIgnoreCase);
        var temp = Path.Combine(Path.GetTempPath(), $"winbackup_{Guid.NewGuid():N}.reg");
        await File.WriteAllTextAsync(temp, text, Encoding.Unicode, token);
        try { await Proc.RunAsync(Proc.SystemExe("reg.exe"), $"import \"{temp}\"", null, token); }
        finally { File.Delete(temp); }
    }

    static void RestoreWallpaper(string dir)
    {
        var folder = Path.Combine(dir, "Papel de parede");
        var image = Directory.Exists(folder) ? Directory.GetFiles(folder).FirstOrDefault() : null;
        if (image == null) return;
        var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Papel de parede (WINbackup)");
        Directory.CreateDirectory(target);
        var path = Path.Combine(target, Path.GetFileName(image));
        File.Copy(image, path, true);
        SystemParametersInfo(0x0014 /* SPI_SETDESKWALLPAPER */, 0, path, 0x01 | 0x02);
    }

    static void BroadcastSettingChange(string area) =>
        SendMessageTimeout((IntPtr)0xFFFF, 0x001A /* WM_SETTINGCHANGE */, IntPtr.Zero, area, 0x0002, 5000, out _);

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint action, uint param, string value, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}
