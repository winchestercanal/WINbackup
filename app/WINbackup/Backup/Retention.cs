namespace WINbackup.Backup;

/// <summary>
/// Retenção: mantém as últimas N versões de cada tipo de backup (o título do manifesto) e apaga as mais
/// antigas. Nunca apaga o último backup completo de cada tipo nem uma versão que guarda arquivos
/// usados por uma versão mantida (o incremental aponta para versões anteriores).
/// </summary>
public static class Retention
{
    /// <summary>Nomes das versões que a retenção apagaria agora (o Histórico mostra em cinza).</summary>
    public static HashSet<string> Removable(string destination, int keep) =>
        Plan(destination, keep).Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

    static List<(string Path, string Name)> Plan(string destination, int keep)
    {
        var versions = Services.Destinations.Versions(destination)
            .Select(v => (Path: v, Name: System.IO.Path.GetFileName(v), M: Manifest.Load(v)))
            .Where(v => v.M is { Completed: true })
            .ToList();

        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in versions.GroupBy(v => v.M!.Title))
        {
            foreach (var v in group.Take(keep)) kept.Add(v.Name);
            if (group.FirstOrDefault(v => v.M!.Kind == "Completo") is { Name: { } full }) kept.Add(full);
        }

        // Versões das quais as mantidas dependem
        foreach (var v in versions.Where(v => kept.Contains(v.Name)).ToList())
            foreach (var f in v.M!.Files)
                kept.Add(f.Stored.Split('\\')[0]);

        return versions.Where(v => !kept.Contains(v.Name)).Select(v => (v.Path, v.Name)).ToList();
    }

    public static List<string> Apply(string destination, int keep, Action<string> log)
    {
        var removed = new List<string>();
        foreach (var v in Plan(destination, keep))
        {
            try
            {
                Directory.Delete(v.Path, true);
                removed.Add(v.Name);
                log($"Retenção: versão antiga removida ({v.Name})");
            }
            catch (Exception ex)
            {
                log($"Retenção: não foi possível remover {v.Name}: {ex.Message}");
            }
        }
        return removed;
    }
}
