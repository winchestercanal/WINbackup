namespace WINbackup.Backup;

/// <summary>
/// O mais recente de cada parte do backup numa pasta de destino, para a restauração completa
/// (assistente Pós-formatação e "--restore --postformat"). Ordem: drivers, programas,
/// configurações do Windows, configurações de apps, arquivos e saves, navegadores.
/// </summary>
public class RestorePlan
{
    public record Part(string Path, Manifest M, List<ManifestFolder> Folders);

    public Part? Drivers, Programs, Windows, Configs, Files, Browsers;

    public static RestorePlan Latest(string destination)
    {
        var plan = new RestorePlan();
        foreach (var path in Services.Destinations.Versions(destination))
        {
            var m = Manifest.Load(path);
            if (m is not { Completed: true }) continue;
            var groups = m.Folders.Select(f => (F: f, G: RestoreModule.GroupOf(m, f))).ToList();
            List<ManifestFolder> Of(params string[] tops) => groups.Where(x => tops.Contains(x.G.Split('\\')[0])).Select(x => x.F).ToList();

            if (plan.Drivers == null && (Directory.Exists(System.IO.Path.Combine(path, "Drivers")) || File.Exists(System.IO.Path.Combine(path, "Drivers_Backup.zip"))))
                plan.Drivers = new(path, m, []);
            if (plan.Programs == null && m.Programs.Any(p => p.Source is "winget" or "msstore")) plan.Programs = new(path, m, []);
            if (plan.Windows == null && m.WindowsSettings.Count > 0) plan.Windows = new(path, m, []);
            if (plan.Configs == null && Of("Criadores", "Configurações") is { Count: > 0 } c) plan.Configs = new(path, m, c);
            if (plan.Files == null && Of("Arquivos", "Saves de jogos") is { Count: > 0 } f) plan.Files = new(path, m, f);
            if (plan.Browsers == null && Of("Navegadores") is { Count: > 0 } b) plan.Browsers = new(path, m, b);
        }
        return plan;
    }

    public bool Empty => Drivers == null && Programs == null && Windows == null && Configs == null && Files == null && Browsers == null;

    /// <summary>
    /// Fontes da restauração completa. Configs de apps e navegadores substituem as padrão criadas na
    /// reinstalação; arquivos pessoais e saves nunca são sobrescritos.
    /// </summary>
    public List<RestoreSource> Sources(bool drivers = true, bool programs = true, bool windows = true, bool configs = true, bool files = true, bool browsers = true)
    {
        var list = new List<RestoreSource>();
        if (drivers && Drivers is { } d) list.Add(new RestoreSource(d.Path, d.M, [], null, true));
        if (programs && Programs is { } p) list.Add(new RestoreSource(p.Path, p.M, [], null, false, Programs: true));
        if (windows && Windows is { } w) list.Add(new RestoreSource(w.Path, w.M, [], null, false, WindowsSettings: true));
        if (configs && Configs is { } c) list.Add(new RestoreSource(c.Path, c.M, Keys(c), null, false, ConflictMode.Overwrite));
        if (files && Files is { } f) list.Add(new RestoreSource(f.Path, f.M, Keys(f), null, false, ConflictMode.Skip));
        if (browsers && Browsers is { } b) list.Add(new RestoreSource(b.Path, b.M, Keys(b), null, false, ConflictMode.Overwrite));
        return list;
    }

    static HashSet<string> Keys(Part p) => p.Folders.Select(f => f.Key).ToHashSet();
}
