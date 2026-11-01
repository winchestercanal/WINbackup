using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using WINbackup.Services;

namespace WINbackup.Backup;

public record BrowserProfile(string Folder, string Name, string Account, string Path, int Extensions);

public record BrowserInfo(string Key, string Name, bool Firefox, string Root, string Exe, string Process, string PasswordUrl,
    string StoreUrl, List<BrowserProfile> Profiles)
{
    public bool Installed => Profiles.Count > 0;
}

/// <summary>
/// Navegadores: perfis do Edge, Chrome, Brave, Vivaldi, Opera e Firefox. Copia favoritos, configurações
/// e (no Firefox) extensões; gera a lista de extensões com link da loja. Senhas nunca são lidas.
/// </summary>
public static class BrowsersModule
{
    /// <summary>O que salvar de cada perfil (só estes itens da pasta do perfil).</summary>
    static readonly string[] ChromiumFiles = ["Bookmarks", "Bookmarks.bak", "Preferences", "Notes", "Custom Dictionary.txt"];
    static readonly string[] FirefoxFiles =
    [
        "places.sqlite", "favicons.sqlite", "prefs.js", "user.js", "extensions", "extensions.json", "extension-preferences.json",
        "extension-settings.json", "browser-extension-data", "containers.json", "search.json.mozlz4", "bookmarkbackups", "chrome",
        "handlers.json", "xulstore.json",
    ];

    record Def(string Key, string Name, bool Firefox, string Root, string Exe, string Process, string PasswordUrl, string StoreUrl);

    static readonly Def[] Defs =
    [
        new("edge", "Microsoft Edge", false, @"%LOCALAPPDATA%\Microsoft\Edge\User Data", "msedge.exe", "msedge",
            "edge://settings/autofill/passwords", "https://microsoftedge.microsoft.com/addons/detail/{0}"),
        new("chrome", "Google Chrome", false, @"%LOCALAPPDATA%\Google\Chrome\User Data", "chrome.exe", "chrome",
            "chrome://password-manager/settings", "https://chromewebstore.google.com/detail/{0}"),
        new("brave", "Brave", false, @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\User Data", "brave.exe", "brave",
            "brave://password-manager/settings", "https://chromewebstore.google.com/detail/{0}"),
        new("vivaldi", "Vivaldi", false, @"%LOCALAPPDATA%\Vivaldi\User Data", "vivaldi.exe", "vivaldi",
            "vivaldi://settings/passwords", "https://chromewebstore.google.com/detail/{0}"),
        new("opera", "Opera", false, @"%APPDATA%\Opera Software\Opera Stable", "opera.exe", "opera",
            "opera://settings/passwords", "https://addons.opera.com/extensions/details/app_id/{0}"),
        new("firefox", "Mozilla Firefox", true, @"%APPDATA%\Mozilla\Firefox", "firefox.exe", "firefox",
            "about:logins", "https://addons.mozilla.org/firefox/addon/{0}"),
    ];

    public static List<BrowserInfo> Detect() => Defs.Select(d =>
    {
        var root = Environment.ExpandEnvironmentVariables(d.Root);
        var profiles = new List<BrowserProfile>();
        try
        {
            if (d.Firefox) profiles = FirefoxProfiles(root);
            else if (d.Key == "opera") { if (File.Exists(Path.Combine(root, "Preferences"))) profiles.Add(new("Opera Stable", "Padrão", "", root, CountChromiumExtensions(root))); }
            else profiles = ChromiumProfiles(root);
        }
        catch (Exception ex) { CrashLog.Write(ex); }
        return new BrowserInfo(d.Key, d.Name, d.Firefox, root, d.Exe, d.Process, d.PasswordUrl, d.StoreUrl, profiles);
    }).ToList();

    /// <summary>Perfis listados em "Local State" → profile.info_cache (nome e conta de cada um).</summary>
    static List<BrowserProfile> ChromiumProfiles(string root)
    {
        var list = new List<BrowserProfile>();
        var localState = Path.Combine(root, "Local State");
        if (!File.Exists(localState)) return list;
        var cache = JsonNode.Parse(ReadShared(localState))?["profile"]?["info_cache"]?.AsObject();
        if (cache == null) return list;
        foreach (var (folder, info) in cache)
        {
            var path = Path.Combine(root, folder);
            if (!Directory.Exists(path)) continue;
            var name = info?["name"]?.GetValue<string>() ?? folder;
            var account = info?["user_name"]?.GetValue<string>() ?? "";
            list.Add(new BrowserProfile(folder, name, account.Length > 0 ? account : "Perfil local", path, CountChromiumExtensions(path)));
        }
        return list.OrderBy(p => p.Folder == "Default" ? "" : p.Folder).ToList();
    }

    static List<BrowserProfile> FirefoxProfiles(string root)
    {
        var list = new List<BrowserProfile>();
        var ini = Path.Combine(root, "profiles.ini");
        if (!File.Exists(ini)) return list;
        string? name = null, path = null;
        var relative = true;
        void Flush()
        {
            if (path == null) return;
            var full = relative ? Path.Combine(root, path.Replace('/', '\\')) : path;
            if (Directory.Exists(full)) list.Add(new BrowserProfile(Path.GetFileName(full), name ?? Path.GetFileName(full), "", full, FirefoxExtensions(full).Count));
            name = path = null;
            relative = true;
        }
        foreach (var raw in File.ReadAllLines(ini))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { Flush(); continue; }
            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var (k, v) = (line[..eq], line[(eq + 1)..]);
            if (k == "Name") name = v;
            else if (k == "Path") path = v;
            else if (k == "IsRelative") relative = v == "1";
        }
        Flush();
        return list;
    }

    static string ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(fs);
        return reader.ReadToEnd();
    }

    // ---------- Extensões ----------

    static int CountChromiumExtensions(string profile) => ChromiumExtensions(profile).Count;

    /// <summary>Extensões instaladas: Extensions\&lt;id&gt;\&lt;versão&gt;\manifest.json (o nome pode vir de _locales).</summary>
    public static List<(string Id, string Name)> ChromiumExtensions(string profile)
    {
        var list = new List<(string, string)>();
        var dir = Path.Combine(profile, "Extensions");
        if (!Directory.Exists(dir)) return list;
        foreach (var ext in Directory.GetDirectories(dir))
        {
            try
            {
                var version = Directory.GetDirectories(ext).OrderByDescending(v => v).FirstOrDefault();
                var manifest = version == null ? null : Path.Combine(version, "manifest.json");
                if (manifest == null || !File.Exists(manifest)) continue;
                var json = JsonNode.Parse(File.ReadAllText(manifest), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                var name = json?["name"]?.GetValue<string>() ?? Path.GetFileName(ext);
                if (name.StartsWith("__MSG_"))
                {
                    var key = name[6..^2];
                    var locale = json?["default_locale"]?.GetValue<string>() ?? "en";
                    var messages = Path.Combine(version!, "_locales", locale, "messages.json");
                    if (File.Exists(messages))
                    {
                        var msgs = JsonNode.Parse(File.ReadAllText(messages))?.AsObject();
                        var hit = msgs?.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
                        name = hit?["message"]?.GetValue<string>() ?? name;
                    }
                }
                list.Add((Path.GetFileName(ext), name));
            }
            catch (Exception) { /* extensão com manifesto estranho: fica de fora da lista */ }
        }
        return list.OrderBy(e => e.Item2, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static List<(string Id, string Name)> FirefoxExtensions(string profile)
    {
        var file = Path.Combine(profile, "extensions.json");
        if (!File.Exists(file)) return new();
        try
        {
            var addons = JsonNode.Parse(ReadShared(file))?["addons"]?.AsArray() ?? [];
            return addons.Where(a => a?["type"]?.GetValue<string>() == "extension" && a?["location"]?.GetValue<string>() == "app-profile")
                .Select(a => (a!["id"]!.GetValue<string>(), a["defaultLocale"]?["name"]?.GetValue<string>() ?? a["id"]!.GetValue<string>()))
                .OrderBy(e => e.Item2, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception) { return new(); }
    }

    // ---------- Backup ----------

    public static string Key(BrowserInfo b, BrowserProfile p) => $"{b.Key}:{p.Folder}";

    public static bool IsSelected(BrowserInfo b, BrowserProfile p) =>
        !Settings.Data.BrowsersOff.Contains(b.Key, StringComparer.OrdinalIgnoreCase) &&
        !Settings.Data.BrowsersOff.Contains(Key(b, p), StringComparer.OrdinalIgnoreCase);

    /// <summary>Pastas a copiar: um item por perfil, mais o arquivo que lista os perfis (Local State / profiles.ini).</summary>
    public static List<FolderSpec> SelectedFolders(List<BrowserInfo>? browsers = null)
    {
        var list = new List<FolderSpec>();
        foreach (var b in browsers ?? Detect())
        {
            var chosen = b.Profiles.Where(p => IsSelected(b, p)).ToList();
            if (chosen.Count == 0) continue;
            var group = Path.Combine("Navegadores", b.Name);
            foreach (var p in chosen)
                list.Add(new FolderSpec("browser:" + p.Path.ToLowerInvariant(), p.Folder, p.Path, false, false, group, b.Firefox ? FirefoxFiles : ChromiumFiles));
            // Sem a lista de perfis, um "Profile 2" restaurado não aparece no navegador
            if (b.Key != "opera")
                list.Add(new FolderSpec("browser:" + b.Root.ToLowerInvariant(), "Lista de perfis", b.Root, false, false, group,
                    b.Firefox ? ["profiles.ini", "installs.ini"] : ["Local State"]));
        }
        foreach (var csv in Settings.Data.PasswordCsvs.Where(File.Exists).Select(Path.GetDirectoryName).Distinct())
            list.Add(new FolderSpec("browser:" + csv!.ToLowerInvariant(), "Senhas exportadas", csv, false, true,
                Path.Combine("Navegadores", "Senhas"), Settings.Data.PasswordCsvs.Where(p => Path.GetDirectoryName(p) == csv).Select(Path.GetFileName).ToArray()!));
        return list;
    }

    public static List<string> Running(IEnumerable<BrowserInfo> browsers) =>
        browsers.Where(b => Process.GetProcessesByName(b.Process).Length > 0).Select(b => b.Name).ToList();

    /// <summary>Navegadores\Extensoes.html: o que reinstalar, com link da loja de cada extensão.</summary>
    public static void WriteExtensionList(string versionPath, List<BrowserInfo> browsers)
    {
        static string H(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder()
            .Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>Extensões · WINbackup</title><style>")
            .Append("body{margin:0;background:#0a0f1c;color:#e8f0ff;font:14px 'Segoe UI',system-ui,sans-serif;padding:32px}h1{font-size:24px;margin:0 0 6px}")
            .Append("h2{font-size:16px;margin:24px 0 8px}p{color:#8ea3c7;margin:0 0 16px}ul{margin:0;padding-left:20px}li{margin:4px 0}a{color:#19e3ff}")
            .Append("</style></head><body><h1>Extensões dos navegadores</h1>")
            .Append("<p>No Firefox as extensões voltam junto com o perfil. Nos outros, entre na sua conta para sincronizar ou instale pelos links abaixo.</p>");
        var any = false;
        foreach (var b in browsers)
            foreach (var p in b.Profiles.Where(p => IsSelected(b, p)))
            {
                var exts = b.Firefox ? FirefoxExtensions(p.Path) : ChromiumExtensions(p.Path);
                if (exts.Count == 0) continue;
                any = true;
                sb.Append($"<h2>{H(b.Name)} · {H(p.Name)}</h2><ul>");
                foreach (var (id, name) in exts)
                    sb.Append($"<li><a href=\"{H(string.Format(b.StoreUrl, Uri.EscapeDataString(id)))}\">{H(name)}</a></li>");
                sb.Append("</ul>");
            }
        if (!any) return;
        sb.Append("</body></html>");
        var dir = Path.Combine(versionPath, "Navegadores");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Extensoes.html"), sb.ToString(), Encoding.UTF8);
    }

    /// <summary>Caminho do executável pelo App Paths do Windows (onde os instaladores se registram).</summary>
    public static string? FindExe(BrowserInfo b)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{b.Exe}");
            if (key?.GetValue(null) is string path && File.Exists(path.Trim('"'))) return path.Trim('"');
        }
        return null;
    }
}
