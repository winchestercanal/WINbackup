using System.Buffers;
using System.IO.Enumeration;
using System.Security.Cryptography;
using WINbackup.Models;

namespace WINbackup.Backup;

/// <summary>
/// Backup de Arquivos pessoais: varre as pastas, aplica as exclusões, copia só o que
/// mudou desde a última versão (incremental), usa Shadow Copy para arquivos em uso
/// e confere tudo com SHA-256 no fim.
/// </summary>
public static class FilesModule
{
    const int Buffer = 1 << 20;
    // Arquivos do OneDrive que só existem na nuvem (FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS / RECALL_ON_OPEN)
    const FileAttributes CloudOnly = (FileAttributes)0x400000 | (FileAttributes)0x40000 | FileAttributes.Offline;

    record Work(ManifestFile Entry, string Source, string Target, FileAttributes Attributes, DateTime Created);

    public static async Task RunAsync(BackupRunner r, StepItem step, StepItem? vssStep, StepItem? verifyStep, Manifest m, CancellationToken token)
    {
        await Task.Yield();
        var spec = r.Spec;
        var versionName = Path.GetFileName(r.VersionPath);
        r.Activity = spec.FilesLabel;
        r.Step(step, StepState.Running, "Calculando…");
        if (vssStep != null) r.Step(vssStep, StepState.Running, "Pronta se precisar");

        // Base para o incremental: a cópia mais recente de cada arquivo entre as versões completas
        // (Arquivos e Criadores podem se alternar no mesmo destino)
        Dictionary<string, ManifestFile> previous = new(StringComparer.OrdinalIgnoreCase);
        if (spec.Incremental)
        {
            foreach (var dir in Services.Destinations.Versions(spec.Destination).Take(30))
            {
                if (string.Equals(dir, r.VersionPath, StringComparison.OrdinalIgnoreCase)) continue;
                var prev = Manifest.Load(dir);
                if (prev is not { Completed: true } || prev.Files.Count == 0) continue;
                var sources = prev.Folders.GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.First().Source);
                foreach (var f in prev.Files)
                    if (sources.TryGetValue(f.Folder, out var src))
                        previous.TryAdd(Path.Combine(src, f.Path), f);
            }
        }

        // 1. Varredura
        var work = new List<Work>();
        int reused = 0, skippedCloud = 0, denied = 0;
        long total = 0;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in spec.Folders)
        {
            r.Checkpoint(token);
            if (!Directory.Exists(folder.Path))
            {
                r.Warn($"Pasta não encontrada, ignorada: {folder.Path}");
                continue;
            }
            var stored = folder.Name;
            for (var i = 2; !usedNames.Add(Path.Combine(folder.Group, stored)); i++) stored = $"{folder.Name} ({i})";
            m.Folders.Add(new ManifestFolder { Key = folder.Key, Name = stored, Source = folder.Path, Group = folder.Group });
            var storedRoot = Path.Combine(folder.Group, stored);
            // A pasta e as subpastas vazias também fazem parte do backup
            Directory.CreateDirectory(Path.Combine(r.VersionPath, storedRoot));
            void OnEmpty(string rel)
            {
                Directory.CreateDirectory(Path.Combine(r.VersionPath, storedRoot, rel));
                m.EmptyFolders.Add(new ManifestDir { Folder = folder.Key, Path = rel });
            }

            foreach (var (file, rel) in Walk(folder.Path, spec.Exclusions, () => denied++, OnEmpty))
            {
                r.Checkpoint(token);
                if ((file.Attributes & CloudOnly) != 0 && !spec.DownloadCloudFiles)
                {
                    skippedCloud++;
                    continue;
                }
                var entry = new ManifestFile { Folder = folder.Key, Path = rel, Size = file.Length, Modified = file.LastWriteTimeUtc };
                if (previous.TryGetValue(file.FullName, out var old) && old.Size == entry.Size && old.Modified == entry.Modified &&
                    File.Exists(Path.Combine(spec.Destination, old.Stored)))
                {
                    entry.Stored = old.Stored;
                    entry.Sha256 = old.Sha256;
                    m.Files.Add(entry);
                    reused++;
                    continue;
                }
                entry.Stored = Path.Combine(versionName, storedRoot, rel);
                work.Add(new Work(entry, file.FullName, Path.Combine(r.VersionPath, storedRoot, rel), file.Attributes, file.CreationTimeUtc));
                total += entry.Size;
                if (work.Count % 500 == 0) r.Step(step, StepState.Running, $"Calculando… {work.Count + reused:N0} arquivos");
            }
        }
        m.BasedOn = m.Files.Select(f => f.Stored.Split(Path.DirectorySeparatorChar)[0]).OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        m.Kind = m.BasedOn != null ? "Incremental" : "Completo";
        if (m.BasedOn != null) r.Info($"Incremental: {reused:N0} arquivos sem mudança desde {m.BasedOn}");
        if (denied > 0) r.Warn($"{denied} pastas sem permissão de leitura foram puladas");
        if (skippedCloud > 0) r.Warn($"{skippedCloud} arquivos só na nuvem (OneDrive) foram pulados");
        r.Info($"{work.Count + reused:N0} arquivos encontrados · {work.Count:N0} para copiar ({Services.Sys.Bytes(total)})" +
               (reused > 0 ? $" · {reused:N0} sem mudança" : ""));

        // 2. Cópia
        long done = 0;
        int copied = 0, failed = 0, viaShadow = 0;
        r.SetPhase(0, total);
        r.Step(step, StepState.Running, "Copiando…");
        using (var shadows = spec.UseShadowCopy ? new ShadowCopies(r) : null)
        {
            foreach (var w in work)
            {
                r.Checkpoint(token);
                r.CurrentFile = w.Entry.Path;
                long fileDone = 0;
                void Advance(long n)
                {
                    fileDone += n;
                    done += n;
                    r.AddBytes(n);
                    r.SetPhase(done, total);
                    r.Progress(step, total == 0 ? 1 : (double)done / total);
                }
                try
                {
                    try
                    {
                        w.Entry.Sha256 = Copy(w.Source, w.Target, spec.VerifyHash, Advance, r, token);
                    }
                    catch (IOException ex) when (IsLocked(ex) && shadows?.Map(w.Source) is { } shadowPath)
                    {
                        done -= fileDone;
                        fileDone = 0;
                        w.Entry.Sha256 = Copy(shadowPath, w.Target, spec.VerifyHash, Advance, r, token);
                        viaShadow++;
                        r.Warn($"Arquivo em uso copiado via VSS: {w.Entry.Path}", report: false);
                    }
                    Finish(w, spec.KeepAcl);
                    m.Files.Add(w.Entry);
                    r.StoredBytes += w.Entry.Size;
                    copied++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    failed++;
                    done += w.Entry.Size - fileDone;
                    r.Error(ex switch
                    {
                        IOException io when IsLocked(io) => $"Arquivo em uso, não foi copiado: {w.Entry.Path}",
                        UnauthorizedAccessException => $"Sem permissão para ler: {w.Entry.Path}",
                        FileNotFoundException or DirectoryNotFoundException => $"Arquivo sumiu durante o backup: {w.Entry.Path}",
                        IOException io when (io.HResult & 0xFFFF) is 112 or 39 => $"Destino sem espaço ao copiar: {w.Entry.Path}",
                        _ => $"Não foi possível copiar {w.Entry.Path}: {ex.Message}",
                    });
                }
                if (copied % 200 == 0) m.Save(r.VersionPath);
            }
            if (vssStep != null)
                r.Step(vssStep, shadows!.Failed > 0 && shadows.Created == 0 ? StepState.Warning : StepState.Done,
                    shadows.Failed > 0 && shadows.Created == 0 ? (Services.Sys.IsAdmin ? "Indisponível" : "Indisponível sem administrador")
                    : shadows.Created == 0 ? "Não foi necessária" : $"Usada em {N(viaShadow, "arquivo", "arquivos")}");
        }
        r.ItemsSaved += copied + reused;
        m.Save(r.VersionPath);

        var summary = N(copied, "copiado", "copiados") + (reused > 0 ? $" · {reused:N0} sem mudança" : "") + (failed > 0 ? $" · {failed} com erro" : "");
        r.Step(step, failed > 0 ? StepState.Warning : StepState.Done, summary);
        r.Ok($"{spec.FilesLabel}: {summary}");
        var detail = N(copied + reused, "arquivo", "arquivos") + (viaShadow > 0 ? $" · {viaShadow} via Shadow Copy" : "") + (failed > 0 ? $" · {failed} com erro" : "");
        r.Modules.Add(new ModuleResult(spec.FilesLabel, detail, failed > 0 ? StepState.Warning : StepState.Done));

        // 3. Verificação
        if (verifyStep == null) return;
        r.Activity = "integridade";
        r.Step(verifyStep, StepState.Running, "Conferindo SHA-256…");
        var toCheck = work.Where(w => w.Entry.Sha256 != null).ToList();
        long vTotal = toCheck.Sum(w => w.Entry.Size), vDone = 0;
        int mismatches = 0;
        r.SetPhase(0, vTotal);
        foreach (var w in toCheck)
        {
            r.Checkpoint(token);
            r.CurrentFile = w.Entry.Path;
            try
            {
                var hash = Hash(w.Target, n => { vDone += n; r.AddBytes(n); r.SetPhase(vDone, vTotal); r.Progress(verifyStep, vTotal == 0 ? 1 : (double)vDone / vTotal); }, r, token);
                if (hash == w.Entry.Sha256) continue;
                r.Warn($"Hash divergente: {w.Entry.Path}, copiado de novo");
                w.Entry.Sha256 = Copy(w.Source, w.Target, true, _ => { }, r, token);
                if (Hash(w.Target, _ => { }, r, token) != w.Entry.Sha256) mismatches++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                mismatches++;
                r.Error($"Não foi possível verificar {w.Entry.Path}: {ex.Message}");
            }
        }
        m.HashChecked = true;
        m.HashMismatches = mismatches;
        m.Save(r.VersionPath);
        r.IntegrityOk = mismatches == 0;
        r.Integrity = mismatches == 0 ? "SHA-256 OK" : $"{mismatches} divergentes";
        r.Step(verifyStep, mismatches == 0 ? StepState.Done : StepState.Error,
            mismatches == 0 ? N(toCheck.Count, "arquivo conferido", "arquivos conferidos") : $"{mismatches} arquivos divergentes");
        if (mismatches == 0) r.Ok("Verificação SHA-256 concluída");
    }

    /// <summary>Quantos arquivos e bytes a pasta tem, já aplicando as exclusões (estimativa da tela).</summary>
    public static (int Files, long Bytes) Measure(string root, List<string> exclusions, CancellationToken token)
    {
        int files = 0;
        long bytes = 0;
        if (!Directory.Exists(root)) return (0, 0);
        foreach (var (file, _) in Walk(root, exclusions, () => { }))
        {
            token.ThrowIfCancellationRequested();
            files++;
            bytes += file.Length;
        }
        return (files, bytes);
    }

    /// <summary>Percorre a pasta sem seguir junções/atalhos de pasta (evita laços como "Meus Documentos\Minhas Músicas").</summary>
    static IEnumerable<(FileInfo File, string Rel)> Walk(string root, List<string> exclusions, Action onDenied, Action<string>? onEmptyDir = null)
    {
        var patterns = exclusions.Select(Environment.ExpandEnvironmentVariables).ToList();
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(root));
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            FileSystemInfo[] entries;
            try { entries = dir.GetFileSystemInfos(); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { onDenied(); continue; }

            var kept = entries.Where(e => !Excluded(e, patterns) && e.LinkTarget == null).ToList();
            if (kept.Count == 0 && dir.FullName.Length > root.TrimEnd('\\').Length)
                onEmptyDir?.Invoke(Path.GetRelativePath(root, dir.FullName));

            foreach (var e in kept)
            {
                if (e is DirectoryInfo d)
                {
                    stack.Push(d);
                }
                else if (e is FileInfo f)
                {
                    yield return (f, Path.GetRelativePath(root, f.FullName));
                }
            }
        }
    }

    static bool Excluded(FileSystemInfo e, List<string> patterns)
    {
        foreach (var p in patterns)
        {
            if (p.Contains('\\'))
            {
                if (FileSystemName.MatchesSimpleExpression(p, e.FullName, true) ||
                    e.FullName.StartsWith(p.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(e.FullName, p.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (FileSystemName.MatchesSimpleExpression(p, e.Name, true))
            {
                return true;
            }
        }
        return false;
    }

    static string N(int n, string one, string many) => n == 1 ? $"1 {one}" : $"{n:N0} {many}";

    static bool IsLocked(IOException ex) => (ex.HResult & 0xFFFF) is 32 or 33;

    static string? Copy(string source, string target, bool hash, Action<long> advance, BackupRunner r, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var buffer = ArrayPool<byte>.Shared.Rent(Buffer);
        try
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, Buffer, FileOptions.SequentialScan);
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, Buffer);
            using var sha = hash ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
            int n;
            while ((n = input.Read(buffer, 0, Buffer)) > 0)
            {
                r.Checkpoint(token);
                output.Write(buffer, 0, n);
                sha?.AppendData(buffer, 0, n);
                advance(n);
            }
            return sha == null ? null : Convert.ToHexString(sha.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static string Hash(string path, Action<long> advance, BackupRunner r, CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Buffer);
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, Buffer, FileOptions.SequentialScan);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int n;
            while ((n = input.Read(buffer, 0, Buffer)) > 0)
            {
                r.Checkpoint(token);
                sha.AppendData(buffer, 0, n);
                advance(n);
            }
            return Convert.ToHexString(sha.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static void Finish(Work w, bool keepAcl)
    {
        File.SetCreationTimeUtc(w.Target, w.Created);
        File.SetLastWriteTimeUtc(w.Target, w.Entry.Modified);
        File.SetAttributes(w.Target, w.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive));
        if (!keepAcl) return;
        try { new FileInfo(w.Target).SetAccessControl(new FileInfo(w.Source).GetAccessControl()); }
        catch (Exception) { /* permissões são um extra: o arquivo já está salvo */ }
    }
}
