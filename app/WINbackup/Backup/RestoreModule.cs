using System.Buffers;
using System.Security.Cryptography;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Backup;

public enum RestoreTarget { Original, OtherUser, Folder }
public enum ConflictMode { Skip, Overwrite, KeepBoth }

/// <summary>Uma versão e o que restaurar dela. Folders null = todas as pastas.</summary>
public record RestoreSource(string VersionPath, Manifest Manifest, HashSet<string>? Folders, HashSet<string>? Files, bool Drivers,
    ConflictMode? Conflict = null, bool Programs = false, bool WindowsSettings = false);

/// <summary>
/// Restauração: copia de volta os arquivos de uma ou mais versões (seguindo os arquivos que
/// o incremental deixou em versões anteriores), confere o SHA-256 e trata os conflitos.
/// </summary>
public static class RestoreModule
{
    const int Buffer = 1 << 20;

    record Work(ManifestFile Entry, string Source, string Target, ConflictMode Conflict);

    /// <summary>Pastas padrão: voltam para onde essas pastas estão hoje (OneDrive incluído).</summary>
    static readonly Dictionary<string, Func<string>> KnownFolders = new()
    {
        ["doc"] = () => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        ["img"] = () => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        ["mus"] = () => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        ["vid"] = () => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        ["dwn"] = () => Sys.Downloads,
        ["dsk"] = () => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        ["ssh"] = () => Path.Combine(Sys.UserProfile, ".ssh"),
    };

    static readonly Dictionary<string, string> KnownNames = new()
    {
        ["doc"] = "Documents", ["img"] = "Pictures", ["mus"] = "Music", ["vid"] = "Videos", ["dwn"] = "Downloads", ["dsk"] = "Desktop", ["ssh"] = ".ssh",
    };

    /// <summary>Pasta do backup onde a pasta está (manifestos antigos não guardavam o grupo).</summary>
    public static string GroupOf(Manifest m, ManifestFolder f)
    {
        var sample = m.Files.FirstOrDefault(x => x.Folder == f.Key);
        if (sample == null) return f.Group;
        var parts = sample.Stored.Split('\\');
        var relParts = sample.Path.Split('\\').Length;
        // Stored = <versão>\<grupo...>\<Nome>\<rel>
        return string.Join('\\', parts.Skip(1).Take(parts.Length - 1 - relParts - 1));
    }

    /// <summary>Para onde vai a pasta, conforme o destino escolhido.</summary>
    public static string TargetFor(Manifest m, ManifestFolder f, RestoreTarget mode, string? targetPath)
    {
        switch (mode)
        {
            case RestoreTarget.Folder:
                return Path.Combine(targetPath!, GroupOf(m, f), f.Name);
            case RestoreTarget.OtherUser:
                if (KnownNames.TryGetValue(f.Key, out var name)) return Path.Combine(targetPath!, name);
                return Rebase(f.Source, m.UserProfile, targetPath!);
            default:
                if (KnownFolders.TryGetValue(f.Key, out var current)) return current();
                return Rebase(f.Source, m.UserProfile, Sys.UserProfile);
        }
    }

    static string Rebase(string path, string oldProfile, string newProfile)
    {
        if (string.IsNullOrEmpty(oldProfile)) return path;
        var old = oldProfile.TrimEnd('\\');
        return path.StartsWith(old + "\\", StringComparison.OrdinalIgnoreCase) || string.Equals(path, old, StringComparison.OrdinalIgnoreCase)
            ? newProfile.TrimEnd('\\') + path[old.Length..]
            : path;
    }

    public static async Task RunAsync(BackupRunner r, StepItem? filesStep, StepItem? prep, StepItem? install, StepItem? programsStep, StepItem? windowsStep, CancellationToken token)
    {
        await Task.Yield();
        var spec = r.Spec;

        // Drivers primeiro: depois de formatar, o Wi-Fi costuma depender deles
        foreach (var src in spec.RestoreSources.Where(s => s.Drivers))
        {
            var folder = Path.Combine(src.VersionPath, "Drivers");
            var zip = Path.Combine(src.VersionPath, "Drivers_Backup.zip");
            var path = Directory.Exists(folder) ? folder : File.Exists(zip) ? zip : null;
            if (path == null || prep == null || install == null) continue;
            await DriversModule.RestoreAsync(r, prep, install, path, token);
            break;
        }
        // Depois os programas: as configurações dos apps precisam deles instalados
        if (programsStep != null)
        {
            if (spec.InstallPackages != null)
                await ProgramsModule.InstallAsync(r, programsStep, spec.InstallPackages, null, token);
            else if (spec.RestoreSources.FirstOrDefault(s => s.Programs) is { } ps)
                await ProgramsModule.InstallAsync(r, programsStep,
                    ps.Manifest.Programs.Where(p => p.Source is "winget" or "msstore").Select(p => (p.Id, p.Name, p.Source)).ToList(),
                    ProgramsModule.ManualListPath(ps.VersionPath), token);
        }
        if (windowsStep != null && spec.RestoreSources.FirstOrDefault(s => s.WindowsSettings) is { } ws)
            await ConfigsModule.RestoreWindowsAsync(r, windowsStep, ws.VersionPath, ws.Manifest, token);
        if (filesStep == null) return;

        // 1. Plano
        r.Activity = L.T("arquivos");
        r.Step(filesStep, StepState.Running, L.T("Preparando…"));
        var destRoot = Path.GetDirectoryName(spec.RestoreSources[0].VersionPath)!;
        var work = new List<Work>();
        var emptyDirs = new List<string>();
        foreach (var src in spec.RestoreSources)
        {
            foreach (var f in src.Manifest.Folders.Where(f => src.Folders == null || src.Folders.Contains(f.Key)))
            {
                var target = TargetFor(src.Manifest, f, spec.Target, spec.TargetPath);
                foreach (var e in src.Manifest.Files.Where(x => x.Folder == f.Key))
                {
                    if (src.Files != null && !src.Files.Contains(f.Key + "|" + e.Path)) continue;
                    work.Add(new Work(e, Path.Combine(destRoot, e.Stored), Path.Combine(target, e.Path), src.Conflict ?? spec.Conflict));
                }
                if (src.Files == null)
                    emptyDirs.AddRange(src.Manifest.EmptyFolders.Where(d => d.Folder == f.Key).Select(d => Path.Combine(target, d.Path)));
            }
        }
        long total = work.Sum(w => w.Entry.Size), done = 0;
        r.Info(L.F("{0:N0} arquivos para restaurar ({1})", work.Count, Sys.Bytes(total)));

        // 2. Cópia
        int restored = 0, skipped = 0, same = 0, failed = 0, mismatch = 0;
        r.SetPhase(0, total);
        r.Step(filesStep, StepState.Running, L.T("Restaurando…"));
        foreach (var w in work)
        {
            r.Checkpoint(token);
            r.CurrentFile = w.Entry.Path;
            long fileDone = 0;
            try
            {
                var target = w.Target;
                if (File.Exists(target))
                {
                    var info = new FileInfo(target);
                    if (info.Length == w.Entry.Size && info.LastWriteTimeUtc == w.Entry.Modified)
                    {
                        same++;
                        Advance(w.Entry.Size);
                        continue;
                    }
                    switch (w.Conflict)
                    {
                        case ConflictMode.Skip:
                            skipped++;
                            Advance(w.Entry.Size);
                            continue;
                        case ConflictMode.KeepBoth:
                            target = FreeName(target);
                            break;
                    }
                }
                if (!File.Exists(w.Source)) throw new FileNotFoundException("Arquivo não encontrado no backup", w.Source);

                var hash = Copy(w.Source, target, n => { fileDone += n; Advance(n); }, r, token);
                File.SetLastWriteTimeUtc(target, w.Entry.Modified);
                if (w.Entry.Sha256 != null && hash != w.Entry.Sha256)
                {
                    mismatch++;
                    r.Error(L.F("Conteúdo diferente do backup (SHA-256): {0}", w.Entry.Path));
                }
                restored++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++;
                Advance(w.Entry.Size - fileDone);
                r.Error(ex switch
                {
                    FileNotFoundException => L.F("Faltando no backup: {0}", w.Entry.Path),
                    UnauthorizedAccessException => L.F("Sem permissão para gravar: {0}", w.Target),
                    IOException io when (io.HResult & 0xFFFF) is 32 or 33 => L.F("Arquivo em uso no destino (feche o programa): {0}", w.Target),
                    _ => L.F("Não foi possível restaurar {0}: {1}", w.Entry.Path, ex.Message),
                });
            }
        }
        foreach (var d in emptyDirs)
            try { Directory.CreateDirectory(d); } catch (Exception) { }

        r.ItemsSaved += restored;
        r.StoredBytes += done;
        r.IntegrityOk = mismatch == 0;
        r.Integrity = mismatch == 0 ? L.T("SHA-256 OK") : $"{mismatch} divergentes";
        var summary = $"{restored:N0} restaurados" + (same > 0 ? L.F(" · {0:N0} já iguais", same) : "") +
                      (skipped > 0 ? L.F(" · {0:N0} pulados (já existiam)", skipped) : "") + (failed > 0 ? L.F(" · {0} com erro", failed) : "");
        var state = failed + mismatch > 0 ? StepState.Warning : StepState.Done;
        r.Step(filesStep, state, summary);
        r.Ok(L.F("Restauração: {0}", summary));
        r.Modules.Add(new ModuleResult(L.T("Arquivos"), summary, state));

        // Saves de jogos: a Steam precisa reconhecer as bibliotecas em outros discos
        foreach (var src in spec.RestoreSources)
        {
            var games = src.Manifest.Folders.Any(f => (src.Folders == null || src.Folders.Contains(f.Key)) && GroupOf(src.Manifest, f).StartsWith("Saves de jogos"));
            var extra = src.Manifest.SteamLibraries.Skip(1).ToList();
            if (games && extra.Count > 0)
                r.Warn(L.F("Bibliotecas da Steam do PC antigo: {0}. Na Steam: Configurações → Armazenamento → Adicionar unidade.", string.Join(", ", extra)));
        }

        void Advance(long n)
        {
            done += n;
            r.AddBytes(n);
            r.SetPhase(done, total);
            r.Progress(filesStep, total == 0 ? 1 : (double)done / total);
        }
    }

    /// <summary>"foto.jpg" → "foto (restaurado).jpg", "foto (restaurado 2).jpg"…</summary>
    static string FreeName(string path)
    {
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var candidate = Path.Combine(dir, $"{name} (restaurado){ext}");
        for (var i = 2; File.Exists(candidate); i++) candidate = Path.Combine(dir, $"{name} (restaurado {i}){ext}");
        return candidate;
    }

    static string Copy(string source, string target, Action<long> advance, BackupRunner r, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target)) File.SetAttributes(target, FileAttributes.Normal);
        var buffer = ArrayPool<byte>.Shared.Rent(Buffer);
        try
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, Buffer, FileOptions.SequentialScan);
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, Buffer);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int n;
            while ((n = input.Read(buffer, 0, Buffer)) > 0)
            {
                r.Checkpoint(token);
                output.Write(buffer, 0, n);
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
}
