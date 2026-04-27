using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PCEval.Models;

namespace PCEval.Services;

/// <summary>Detects RAM, storage, and GPU information on Windows.</summary>
public class SystemInfoService : ISystemInfoService
{
    public Task<SystemInfo> GetSystemInfoAsync() => Task.Run(GetSystemInfoSync);

    private SystemInfo GetSystemInfoSync()
    {
        var info = new SystemInfo();
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                PopulateWindows(info);
        }
        catch { }
        return info;
    }

    private static string Run(string file, string args, int timeoutMs = 8000)
    {
        try
        {
            using var p = new System.Diagnostics.Process();
            p.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName               = file,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };
            p.Start();
            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return "";
            }
            stdoutTask.Wait(500);
            return stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "";
        }
        catch { return ""; }
    }

    private static void PopulateWindows(SystemInfo info)
    {
        // ── RAM ───────────────────────────────────────────────────────────
        // Win32_PhysicalMemory: Capacity (bytes), Speed (MHz), SMBIOSMemoryType
        string mem = Run("powershell",
            "-NoProfile -Command \"Get-CimInstance Win32_PhysicalMemory | " +
            "Select-Object Capacity,Speed,SMBIOSMemoryType,ConfiguredClockSpeed | Format-List\"");

        long totalBytes = 0;
        int? maxSpeed = null;
        int? memTypeCode = null;
        foreach (Match m in Regex.Matches(mem, @"Capacity\s*:\s*(\d+)"))
            if (long.TryParse(m.Groups[1].Value, out long b)) totalBytes += b;
        foreach (Match m in Regex.Matches(mem, @"Speed\s*:\s*(\d+)"))
            if (int.TryParse(m.Groups[1].Value, out int s)) maxSpeed = Math.Max(maxSpeed ?? 0, s);
        var mt = Regex.Match(mem, @"SMBIOSMemoryType\s*:\s*(\d+)");
        if (mt.Success && int.TryParse(mt.Groups[1].Value, out int code)) memTypeCode = code;

        if (totalBytes > 0) info.TotalRamGb = Math.Round(totalBytes / (1024.0 * 1024 * 1024), 1);
        if (maxSpeed.HasValue) info.RamSpeedMhz = maxSpeed;
        info.RamType = memTypeCode switch
        {
            // SMBIOS Memory Device — Type
            20 => "DDR",
            21 => "DDR2",
            24 => "DDR3",
            26 => "DDR4",
            27 => "LPDDR",
            28 => "LPDDR2",
            29 => "LPDDR3",
            30 => "LPDDR4",
            34 => "DDR5",
            35 => "LPDDR5",
            _  => null,
        };

        // ── Storage ───────────────────────────────────────────────────────
        // Get-PhysicalDisk: Size (bytes), MediaType (SSD/HDD/Unspecified), BusType
        string disk = Run("powershell",
            "-NoProfile -Command \"Get-PhysicalDisk | " +
            "Select-Object Size,MediaType,BusType,FriendlyName | Format-List\"");

        double total = 0;
        double? primarySize = null;
        string? primaryType = null;
        var blocks = Regex.Split(disk, @"(?:\r?\n){2,}");
        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;
            var sm = Regex.Match(block, @"Size\s*:\s*(\d+)");
            if (!sm.Success || !long.TryParse(sm.Groups[1].Value, out long bytes) || bytes <= 0)
                continue;
            double gb = bytes / (1024.0 * 1024 * 1024);
            total += gb;

            string media = Regex.Match(block, @"MediaType\s*:\s*(\S+)").Groups[1].Value.Trim();
            string bus   = Regex.Match(block, @"BusType\s*:\s*(\S+)").Groups[1].Value.Trim();
            string label = bus.Equals("NVMe", StringComparison.OrdinalIgnoreCase) ? "NVMe SSD"
                         : media.Equals("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD"
                         : media.Equals("HDD", StringComparison.OrdinalIgnoreCase) ? "HDD"
                         : "Storage";

            if (primarySize is null || gb > primarySize)
            {
                primarySize = gb;
                primaryType = label;
            }
        }
        if (total > 0) info.TotalStorageGb = Math.Round(total, 0);
        if (primarySize.HasValue) info.PrimaryStorageGb = Math.Round(primarySize.Value, 0);
        info.PrimaryStorageType = primaryType;

        // ── GPU ───────────────────────────────────────────────────────────
        // Win32_VideoController: Name, AdapterRAM (bytes, capped at ~4GB on older WMI)
        string gpu = Run("powershell",
            "-NoProfile -Command \"Get-CimInstance Win32_VideoController | " +
            "Select-Object Name,AdapterRAM,DriverVersion | Format-List\"");

        // Pick the most likely 'main' GPU (highest reported VRAM, or last enumerated).
        string? bestName = null;
        double? bestVram = null;
        var gpuBlocks = Regex.Split(gpu, @"(?:\r?\n){2,}");
        foreach (var block in gpuBlocks)
        {
            var nm = Regex.Match(block, @"Name\s*:\s*(.+)");
            if (!nm.Success) continue;
            string name = nm.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(name) || name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                continue;

            var vm = Regex.Match(block, @"AdapterRAM\s*:\s*(\d+)");
            double? vram = null;
            if (vm.Success && long.TryParse(vm.Groups[1].Value, out long vb) && vb > 0)
                vram = Math.Round(vb / (1024.0 * 1024 * 1024), 1);

            if (bestName is null || (vram.HasValue && (!bestVram.HasValue || vram > bestVram)))
            {
                bestName = name;
                bestVram = vram;
            }
        }

        // For NVIDIA RTX cards, AdapterRAM is often capped at ~4 GB by WMI;
        // try nvidia-smi for an authoritative VRAM number.
        if (!string.IsNullOrEmpty(bestName) &&
            bestName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            string smi = Run("nvidia-smi", "--query-gpu=memory.total --format=csv,noheader,nounits");
            var sm = Regex.Match(smi, @"(\d+)");
            if (sm.Success && int.TryParse(sm.Groups[1].Value, out int mb) && mb > 0)
                bestVram = Math.Round(mb / 1024.0, 1);
        }

        info.PrimaryGpuName    = bestName;
        info.PrimaryGpuVramGb  = bestVram;
        info.IsDiscreteGpu     = ClassifyDiscrete(bestName);
    }

    private static bool? ClassifyDiscrete(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string n = name.ToLowerInvariant();
        // Integrated heuristics
        if (n.Contains("intel") && (n.Contains("uhd") || n.Contains("iris") || n.Contains("hd graphics") || n.Contains("arc graphics")))
            return false;
        if (n.Contains("amd") && (n.Contains("radeon graphics") || n.Contains("vega") && n.Contains("ryzen")))
            return false;
        if (n.Contains("apple") || n.Contains("qualcomm") || n.Contains("adreno"))
            return false;
        // Discrete heuristics
        if (n.Contains("geforce") || n.Contains("rtx") || n.Contains("gtx")) return true;
        if (n.Contains("radeon rx") || n.Contains("radeon pro")) return true;
        if (n.Contains("arc a") || n.Contains("arc b")) return true;   // Intel Arc discrete
        return null;
    }
}
