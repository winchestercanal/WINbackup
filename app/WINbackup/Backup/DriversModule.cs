using System.IO.Compression;
using System.Management;
using System.Text.Json;
using WINbackup.Models;
using WINbackup.Services;

namespace WINbackup.Backup;

public record DriverInfo(string Inf, string Device, int DeviceCount, string Provider, string Class, string Version, bool ThirdParty)
{
    /// <summary>
    /// Ninguém sabe ao certo o que o Windows Update vai achar. Regra simples: o que já vem
    /// com o Windows ou é publicado pela Microsoft volta sozinho; o resto é melhor guardar.
    /// </summary>
    public bool LikelyOnWindowsUpdate => !ThirdParty || Provider.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Backup de drivers com PnPUtil (/export-driver) e restauração com /add-driver /install.</summary>
public static class DriversModule
{
    public static Task<List<DriverInfo>> ListAsync() => Task.Run(() =>
    {
        var rows = new List<(string Inf, string Device, string Provider, string Class, string Version)>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceName, DeviceClass, DriverVersion, InfName, DriverProviderName, Manufacturer FROM Win32_PnPSignedDriver");
        foreach (ManagementObject mo in searcher.Get())
        {
            using (mo)
            {
                var inf = mo["InfName"] as string;
                if (string.IsNullOrEmpty(inf)) continue;
                var provider = mo["DriverProviderName"] as string;
                if (string.IsNullOrWhiteSpace(provider)) provider = mo["Manufacturer"] as string;
                rows.Add((inf, (mo["DeviceName"] as string ?? "").Trim(), provider?.Trim() ?? "", mo["DeviceClass"] as string ?? "",
                    mo["DriverVersion"] as string ?? ""));
            }
        }

        var list = new List<DriverInfo>();
        // Terceiros: um pacote oemNN.inf pode atender vários dispositivos
        foreach (var g in rows.Where(r => IsThirdParty(r.Inf)).GroupBy(r => r.Inf, StringComparer.OrdinalIgnoreCase))
        {
            var named = g.Where(r => r.Device.Length > 0).Select(r => r.Device).Distinct().ToList();
            var first = g.First();
            list.Add(new DriverInfo(g.Key, named.FirstOrDefault() ?? g.Key, Math.Max(1, named.Count), first.Provider, first.Class, first.Version, true));
        }
        foreach (var r in rows.Where(r => !IsThirdParty(r.Inf) && r.Device.Length > 0)
                              .DistinctBy(r => (r.Device, r.Class)))
            list.Add(new DriverInfo(r.Inf, r.Device, 1, r.Provider, r.Class, r.Version, false));

        return list.OrderByDescending(d => d.ThirdParty).ThenBy(d => d.LikelyOnWindowsUpdate)
                   .ThenBy(d => d.Class).ThenBy(d => d.Device).ToList();
    });

    static readonly Dictionary<string, string> ClassNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DISPLAY"] = "Vídeo", ["NET"] = "Rede", ["MEDIA"] = "Áudio e vídeo", ["SYSTEM"] = "Sistema", ["BLUETOOTH"] = "Bluetooth",
        ["HIDCLASS"] = "HID", ["USB"] = "USB", ["USBDEVICE"] = "USB", ["BIOMETRIC"] = "Biometria", ["KEYBOARD"] = "Teclado",
        ["MOUSE"] = "Mouse", ["CAMERA"] = "Câmera", ["IMAGE"] = "Câmera / scanner", ["SCSIADAPTER"] = "Armazenamento",
        ["HDC"] = "Armazenamento", ["DISKDRIVE"] = "Disco", ["PRINTER"] = "Impressora", ["PRINTQUEUE"] = "Impressora",
        ["AUDIOPROCESSINGOBJECT"] = "Áudio (efeitos)", ["AUDIOENDPOINT"] = "Áudio", ["EXTENSION"] = "Extensão",
        ["SOFTWARECOMPONENT"] = "Componente", ["SOFTWAREDEVICE"] = "Dispositivo de software", ["COMPUTEACCELERATOR"] = "Acelerador (IA)",
        ["FIRMWARE"] = "Firmware", ["MONITOR"] = "Monitor", ["SECURITYDEVICES"] = "Segurança (TPM)", ["PORTS"] = "Portas",
        ["SDHOST"] = "Leitor de cartão", ["SMARTCARDREADER"] = "Leitor de smart card", ["ANDROIDUSBDEVICECLASS"] = "Android (ADB)",
        ["NETSERVICE"] = "Serviço de rede", ["NETTRANS"] = "Protocolo de rede", ["WPD"] = "Celular / MTP", ["BATTERY"] = "Bateria",
        ["SENSOR"] = "Sensor", ["PROCESSOR"] = "Processador", ["VOLUME"] = "Volume", ["SOUND"] = "Áudio",
    };

    public static string FriendlyClass(string cls) =>
        ClassNames.TryGetValue(cls, out var n) ? L.T(n) : cls.Length == 0 ? "—" : char.ToUpper(cls[0]) + cls[1..].ToLowerInvariant();

    static bool IsThirdParty(string inf) => inf.StartsWith("oem", StringComparison.OrdinalIgnoreCase);

    public static async Task BackupAsync(BackupRunner r, StepItem step, Manifest m, CancellationToken token)
    {
        r.Activity = L.T("Drivers");
        r.CurrentFile = "";
        r.Step(step, StepState.Running, L.T("Lendo drivers instalados…"));
        if (!Sys.IsAdmin)
        {
            r.Step(step, StepState.Error, L.T("Precisa de administrador"));
            r.Error(L.T("O backup de drivers precisa que o WINbackup rode como administrador."));
            r.Modules.Add(new ModuleResult(L.T("Drivers"), L.T("Não feito: precisa de administrador"), StepState.Error));
            return;
        }

        var all = await ListAsync();
        var chosen = all.Where(d => d.ThirdParty && (r.Spec.DriverInfs == null ||
                         r.Spec.DriverInfs.Contains(d.Inf, StringComparer.OrdinalIgnoreCase))).ToList();
        if (chosen.Count == 0)
        {
            r.Step(step, StepState.Done, L.T("Nenhum driver de terceiros"));
            r.Modules.Add(new ModuleResult(L.T("Drivers"), L.T("Nenhum driver de terceiros encontrado"), StepState.Done));
            return;
        }

        // Pasta solta (sem zip): dá para abrir, ver e reinstalar um driver só
        var folder = Path.Combine(r.VersionPath, "Drivers");
        Directory.CreateDirectory(folder);
        int ok = 0;
        for (var i = 0; i < chosen.Count; i++)
        {
            r.Checkpoint(token);
            var d = chosen[i];
            r.CurrentFile = $"{d.Device} ({d.Inf})";
            r.Step(step, StepState.Running, L.F("Exportando {0} de {1}…", i + 1, chosen.Count));
            var target = Path.Combine(folder, Path.GetFileNameWithoutExtension(d.Inf) + "_" + Safe(d.Device));
            // O PnPUtil não cria a pasta de destino: sem ela devolve o código 2 (caminho não encontrado)
            Directory.CreateDirectory(target);
            var output = new List<string>();
            var code = await Proc.RunAsync(Proc.SystemExe("pnputil.exe"), $"/export-driver {d.Inf} \"{target}\"", output.Add, token);
            if (code == 0)
            {
                ok++;
                m.Drivers.Add(new ManifestDriver { Inf = d.Inf, Device = d.Device, Provider = d.Provider, Class = d.Class, Version = d.Version });
            }
            else
            {
                r.Warn(L.F("Driver não exportado: {0} ({1}), código {2}. {3}", d.Device, d.Inf, code, output.LastOrDefault()));
            }
            r.Progress(step, (i + 1.0) / chosen.Count);
        }

        if (ok == 0)
        {
            Directory.Delete(folder, true);
            r.Step(step, StepState.Error, L.T("Nenhum driver exportado"));
            r.Error(L.T("Nenhum driver foi exportado. Veja os avisos no log para o motivo de cada um."));
            r.Modules.Add(new ModuleResult(L.T("Drivers"), L.F("0 de {0} exportados", chosen.Count), StepState.Error));
            return;
        }

        File.WriteAllText(Path.Combine(folder, "drivers.json"), JsonSerializer.Serialize(m.Drivers, new JsonSerializerOptions { WriteIndented = true }));
        // Pastas de drivers que falharam ficam vazias: não servem para nada
        foreach (var dir in Directory.GetDirectories(folder).Where(d => !Directory.EnumerateFileSystemEntries(d).Any()))
            Directory.Delete(dir);
        var size = new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        r.StoredBytes += size;
        r.ItemsSaved += ok;

        var failed = chosen.Count - ok;
        var status = L.F("{0} drivers de terceiros", ok) + (failed > 0 ? L.F(" · {0} com erro", failed) : "");
        r.Step(step, failed > 0 ? StepState.Warning : StepState.Done, status);
        if (failed > 0) r.Warn(L.F("{0} drivers não foram exportados. Veja o log para o motivo de cada um.", failed));
        r.Ok(L.F("Drivers: {0} na pasta Drivers ({1})", status, Sys.Bytes(size)));
        r.Modules.Add(new ModuleResult(L.T("Drivers"), status, failed > 0 ? StepState.Warning : StepState.Done));
    }

    public static async Task RestoreAsync(BackupRunner r, StepItem prep, StepItem install, string source, CancellationToken token)
    {
        r.Activity = L.T("Drivers");
        r.Step(prep, StepState.Running, L.T("Abrindo o backup…"));
        if (!Sys.IsAdmin)
        {
            r.Step(prep, StepState.Error, L.T("Precisa de administrador"));
            r.Error(L.T("Instalar drivers precisa que o WINbackup rode como administrador."));
            r.Modules.Add(new ModuleResult(L.T("Drivers"), L.T("Não feito: precisa de administrador"), StepState.Error));
            return;
        }

        string folder;
        string? temp = null;
        if (File.Exists(source) && source.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            temp = Path.Combine(Path.GetTempPath(), "WINbackup_drivers_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            r.CurrentFile = Path.GetFileName(source);
            r.Info(L.F("Extraindo {0} para uma pasta temporária", source));
            await Task.Run(() => ZipFile.ExtractToDirectory(source, temp, overwriteFiles: true), token);
            folder = temp;
        }
        else if (Directory.Exists(source))
        {
            folder = source;
        }
        else
        {
            throw new FileNotFoundException(L.T("Não encontrei o backup de drivers: ") + source);
        }

        try
        {
            var infs = Directory.EnumerateFiles(folder, "*.inf", SearchOption.AllDirectories).ToList();
            r.Step(prep, StepState.Done, $"{infs.Count} arquivos .inf");
            if (infs.Count == 0)
            {
                r.Step(install, StepState.Error, L.T("Nenhum .inf encontrado"));
                r.Error(L.T("Nenhum arquivo .inf foi encontrado no backup escolhido."));
                r.Modules.Add(new ModuleResult(L.T("Drivers"), L.T("Nenhum .inf encontrado"), StepState.Error));
                return;
            }

            r.Step(install, StepState.Running, L.T("Instalando…"));
            int seen = 0;
            var code = await Proc.RunAsync(Proc.SystemExe("pnputil.exe"), $"/add-driver \"{Path.Combine(folder, "*.inf")}\" /subdirs /install", line =>
            {
                r.Info(line);
                if (line.Contains(".inf", StringComparison.OrdinalIgnoreCase))
                {
                    seen++;
                    r.CurrentFile = Path.GetFileName(line.Split(':').Last().Trim());
                    r.Progress(install, Math.Min(0.95, (double)seen / infs.Count));
                }
            }, token);

            r.ItemsSaved = infs.Count;
            switch (code)
            {
                case 0:
                    r.Step(install, StepState.Done, L.T("Drivers instalados"));
                    r.Modules.Add(new ModuleResult(L.T("Drivers"), L.F("{0} pacotes processados", infs.Count), StepState.Done));
                    break;
                case 3010:
                    r.RebootRequired = true;
                    r.Step(install, StepState.Done, L.T("Instalados · reinício necessário"));
                    r.Warn(L.T("Reinicie o computador para terminar a instalação dos drivers."));
                    r.Modules.Add(new ModuleResult(L.T("Drivers"), L.F("{0} pacotes · reinício necessário", infs.Count), StepState.Warning));
                    break;
                case 259:
                    r.Step(install, StepState.Warning, L.T("Nada novo para instalar"));
                    r.Warn(L.T("Nenhum driver foi instalado: eles já estavam presentes ou não servem para este PC."));
                    r.Modules.Add(new ModuleResult(L.T("Drivers"), L.T("Nenhum driver novo instalado"), StepState.Warning));
                    break;
                default:
                    r.Step(install, StepState.Warning, L.F("Terminou com código {0}", code));
                    r.Warn(L.F("O PnPUtil terminou com o código {0}. Veja o log para os detalhes.", code));
                    r.Modules.Add(new ModuleResult(L.T("Drivers"), L.F("PnPUtil terminou com o código {0}", code), StepState.Warning));
                    break;
            }
        }
        finally
        {
            if (temp != null) try { Directory.Delete(temp, true); } catch { }
        }
    }

    static string Safe(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var s = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return s.Length > 40 ? s[..40] : s;
    }
}
