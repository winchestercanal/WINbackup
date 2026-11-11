using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Backup;

public record InstalledProgram(string Name, string Id, string Version, string Available, string Source)
{
    /// <summary>Tem pacote no winget ou na Microsoft Store: dá para reinstalar sozinho.</summary>
    public bool Packaged => Source is "winget" or "msstore";
}

/// <summary>
/// Programas: lista o que está instalado com o winget, salva o Programas.json (formato do
/// "winget import") e a lista dos sem pacote, e reinstala um por um na restauração.
/// </summary>
public static class ProgramsModule
{
    // Códigos do winget para "já instalado / nada para atualizar"
    const int AlreadyInstalled = unchecked((int)0x8A150061), NoApplicableUpgrade = unchecked((int)0x8A15002B);
    const string Folder = "Programas", ImportFile = "Programas.json", ManualFile = "Instalar_manualmente.html";

    /// <summary>Componentes do sistema e runtimes sem pacote: não fazem sentido na lista manual.</summary>
    static readonly string[] Noise =
    [
        "Microsoft Visual C++", "Microsoft .NET", "Microsoft Windows Desktop Runtime", "Microsoft ASP.NET", "Windows Software Development Kit",
        "Microsoft Update Health Tools", "Microsoft Edge WebView2", "Windows Driver Package", "Update for", "Microsoft Windows SDK",
    ];

    public static async Task<List<InstalledProgram>> ListAsync(CancellationToken token)
    {
        var winget = Proc.Winget() ?? throw new FileNotFoundException(
            L.T("O winget não foi encontrado. Instale ou atualize o \"Instalador de Aplicativo\" pela Microsoft Store."));
        var lines = new List<string>();
        await Proc.RunAsync(winget, "list --accept-source-agreements --disable-interactivity", lines.Add, token, Encoding.UTF8);
        return Parse(lines);
    }

    /// <summary>
    /// Lê a tabela do "winget list" pela posição das colunas (os títulos vêm traduzidos).
    /// Colunas: Nome, ID, Versão, [Disponível], Origem.
    /// </summary>
    public static List<InstalledProgram> Parse(List<string> lines)
    {
        var dash = lines.FindIndex(l => l.Length > 20 && l.Trim().All(c => c == '-'));
        if (dash < 1) return new();
        var header = lines[dash - 1];
        var starts = new List<int> { 0 };
        for (var i = 1; i < header.Length; i++)
            if (header[i] != ' ' && header[i - 1] == ' ' && i >= 2 && header[i - 2] == ' ') starts.Add(i);
        // Proc.RunAsync apara as linhas: o cabeçalho pode ter perdido espaços à esquerda (nenhum caso hoje)
        if (starts.Count < 4) return new();
        var hasAvailable = starts.Count >= 5;

        string Col(string line, int index)
        {
            if (index >= starts.Count || starts[index] >= line.Length) return "";
            var end = index + 1 < starts.Count ? Math.Min(starts[index + 1], line.Length) : line.Length;
            return line[starts[index]..end].Trim();
        }

        var list = new List<InstalledProgram>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(dash + 1))
        {
            if (line.Length < starts[2]) continue;
            var id = Col(line, 1);
            var name = Col(line, 0);
            if (id.Length == 0 || name.Length == 0 || !seen.Add(id)) continue;
            var source = Col(line, hasAvailable ? 4 : 3);
            var program = new InstalledProgram(name, id, Col(line, 2), hasAvailable ? Col(line, 3) : "", source);
            if (!program.Packaged && (id.StartsWith(@"MSIX\", StringComparison.OrdinalIgnoreCase) ||
                                      Noise.Any(n => name.StartsWith(n, StringComparison.OrdinalIgnoreCase)))) continue;
            list.Add(program);
        }
        return list.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // ---------- Backup ----------

    public static async Task BackupAsync(BackupRunner r, StepItem step, Manifest m, CancellationToken token)
    {
        r.Activity = L.T("Programas");
        r.CurrentFile = "";
        r.Step(step, StepState.Running, L.T("Lendo programas instalados…"));
        var winget = Proc.Winget();
        if (winget == null)
        {
            r.Step(step, StepState.Error, L.T("winget não encontrado"));
            r.Error(L.T("O winget não foi encontrado. Instale ou atualize o \"Instalador de Aplicativo\" pela Microsoft Store."));
            r.Modules.Add(new ModuleResult(L.T("Programas"), L.T("winget não encontrado"), StepState.Error));
            return;
        }

        var installed = await ListAsync(token);
        r.Progress(step, 0.4);
        var chosen = installed.Where(p => p.Packaged && !r.Spec.ProgramsOff.Contains(p.Id)).ToList();
        var manual = installed.Where(p => !p.Packaged).ToList();

        var folder = Path.Combine(r.VersionPath, Folder);
        Directory.CreateDirectory(folder);
        r.Step(step, StepState.Running, L.T("Exportando a lista do winget…"));
        var export = Path.Combine(folder, "winget_export.json");
        var output = new List<string>();
        await Proc.RunAsync(winget, $"export -o \"{export}\" --accept-source-agreements --disable-interactivity", output.Add, token, Encoding.UTF8);
        r.Progress(step, 0.85);

        var exported = 0;
        if (File.Exists(export))
        {
            exported = WriteImportFile(export, Path.Combine(folder, ImportFile), chosen.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase));
            File.Delete(export);
        }
        else
        {
            r.Warn(L.T("O winget export não gerou o arquivo: ") + string.Join(" ", output.TakeLast(2)));
        }
        WriteManualList(Path.Combine(folder, ManualFile), manual);

        m.Programs = chosen.Concat(manual)
            .Select(p => new ManifestProgram { Name = p.Name, Id = p.Id, Version = p.Version, Source = p.Source })
            .ToList();
        r.ItemsSaved += exported;
        r.StoredBytes += new DirectoryInfo(folder).EnumerateFiles().Sum(f => f.Length);

        var status = L.F("{0} no winget", exported) + (manual.Count > 0 ? L.F(" · {0} manuais", manual.Count) : "");
        r.Step(step, exported > 0 ? StepState.Done : StepState.Warning, status);
        r.Ok(L.F("Programas: {0}", status));
        if (manual.Count > 0)
            r.Warn(L.F("{0} programas não têm pacote no winget. A lista para instalar à mão foi salva em {1}.", manual.Count, ManualFile));
        r.Modules.Add(new ModuleResult(L.T("Programas"), status, manual.Count > 0 ? StepState.Warning : StepState.Done));
    }

    /// <summary>"Exportar lista (.json)": Programas.json com os pacotes escolhidos, onde o usuário quiser.</summary>
    public static async Task<int> ExportAsync(string targetFile, HashSet<string> ids, CancellationToken token)
    {
        var winget = Proc.Winget() ?? throw new FileNotFoundException(L.T("O winget não foi encontrado."));
        var temp = Path.Combine(Path.GetTempPath(), $"winbackup_export_{Guid.NewGuid():N}.json");
        await Proc.RunAsync(winget, $"export -o \"{temp}\" --accept-source-agreements --disable-interactivity", null, token, Encoding.UTF8);
        if (!File.Exists(temp)) throw new IOException(L.T("O winget export não gerou o arquivo."));
        try { return WriteImportFile(temp, targetFile, ids); }
        finally { File.Delete(temp); }
    }

    /// <summary>Copia o export do winget mantendo só os pacotes escolhidos (o formato é o do "winget import").</summary>
    static int WriteImportFile(string exportPath, string target, HashSet<string> ids)
    {
        var root = JsonNode.Parse(File.ReadAllText(exportPath))!;
        var count = 0;
        foreach (var source in root["Sources"]?.AsArray() ?? [])
        {
            var packages = source!["Packages"]!.AsArray();
            foreach (var p in packages.ToList())
            {
                if (ids.Contains(p!["PackageIdentifier"]!.GetValue<string>())) count++;
                else packages.Remove(p);
            }
        }
        File.WriteAllText(target, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return count;
    }

    static void WriteManualList(string path, List<InstalledProgram> manual)
    {
        static string H(string s) => WebUtility.HtmlEncode(s);
        var sb = new StringBuilder()
            .Append($"<!doctype html><html lang=\"{L.Culture.Name}\"><head><meta charset=\"utf-8\"><title>{H(L.T("Instalar manualmente"))} · WINbackup</title><style>")
            .Append("body{margin:0;background:#0a0f1c;color:#e8f0ff;font:14px 'Segoe UI',system-ui,sans-serif;padding:32px}h1{font-size:24px;margin:0 0 6px}")
            .Append("p{color:#8ea3c7;margin:0 0 20px}table{width:100%;border-collapse:collapse;background:#0e1526;border:1px solid #1c2a48}")
            .Append("td,th{padding:10px 16px;border-bottom:1px solid #16223d;text-align:left}th{color:#8ea3c7;font-weight:400;font-size:12px}")
            .Append("a{color:#19e3ff}</style></head><body>")
            .Append($"<h1>{H(L.T("Programas para instalar à mão"))}</h1><p>{H(L.F("{0} programas não têm pacote no winget nem na Microsoft Store. Baixe cada um no site do fabricante.", manual.Count))}</p>")
            .Append($"<table><tr><th>{H(L.T("Programa"))}</th><th>{H(L.T("Versão"))}</th><th></th></tr>");
        foreach (var p in manual)
            sb.Append($"<tr><td>{H(p.Name)}</td><td>{H(p.Version)}</td><td><a href=\"https://www.google.com/search?q={WebUtility.UrlEncode(p.Name + " download")}\">{H(L.T("Procurar download"))}</a></td></tr>");
        sb.Append("</table></body></html>");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    // ---------- Restauração ----------

    public static string? ManualListPath(string versionPath)
    {
        var path = Path.Combine(versionPath, Folder, ManualFile);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Lê um Programas.json (formato do winget import) e devolve id e origem de cada pacote.</summary>
    public static List<(string Id, string Source)> ReadImportFile(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        var list = new List<(string, string)>();
        foreach (var source in root["Sources"]?.AsArray() ?? [])
        {
            var name = source!["SourceDetails"]?["Name"]?.GetValue<string>() ?? "winget";
            foreach (var p in source["Packages"]!.AsArray())
                list.Add((p!["PackageIdentifier"]!.GetValue<string>(), name));
        }
        return list;
    }

    /// <summary>Instala um pacote por vez: dá progresso real e o resultado de cada programa.</summary>
    public static async Task InstallAsync(BackupRunner r, StepItem step, List<(string Id, string Name, string Source)> packages, string? manualList, CancellationToken token)
    {
        r.Activity = L.T("Programas");
        var winget = Proc.Winget();
        if (winget == null)
        {
            r.Step(step, StepState.Error, L.T("winget não encontrado"));
            r.Error(L.T("O winget não foi encontrado. Abra a Microsoft Store, atualize o \"Instalador de Aplicativo\" e tente de novo."));
            r.Modules.Add(new ModuleResult(L.T("Programas"), L.T("winget não encontrado"), StepState.Error));
            return;
        }

        int ok = 0, already = 0, failed = 0;
        for (var i = 0; i < packages.Count; i++)
        {
            r.Checkpoint(token);
            var (id, name, source) = packages[i];
            r.CurrentFile = name;
            r.Step(step, StepState.Running, L.F("Instalando {0} de {1}…", i + 1, packages.Count));
            var output = new List<string>();
            var code = await Proc.RunAsync(winget,
                $"install --id \"{id}\" --exact --source {source} --accept-package-agreements --accept-source-agreements --disable-interactivity --silent",
                output.Add, token, Encoding.UTF8);
            if (code == 0) { ok++; r.Ok(L.F("Instalado: {0}", name)); }
            else if (code is AlreadyInstalled or NoApplicableUpgrade) { already++; r.Info(L.F("Já instalado: {0}", name)); }
            else
            {
                failed++;
                r.Warn(L.F("Não instalou {0} ({1}): código 0x{2:X8}. {3}", name, id, code, output.LastOrDefault(l => l.Length > 3)));
            }
            r.Progress(step, (i + 1.0) / packages.Count);
        }

        r.ItemsSaved += ok;
        var status = $"{ok} instalados" + (already > 0 ? L.F(" · {0} já estavam", already) : "") + (failed > 0 ? L.F(" · {0} com erro", failed) : "");
        r.Step(step, failed > 0 ? StepState.Warning : StepState.Done, status);
        r.Ok(L.F("Programas: {0}", status));
        if (manualList != null)
            r.Warn(L.F("Alguns programas precisam de instalação manual: veja {0}", manualList));
        r.Modules.Add(new ModuleResult(L.T("Programas"), status, failed > 0 ? StepState.Warning : StepState.Done));
    }
}
