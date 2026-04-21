using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PCEval.Models;

namespace PCEval.Services;

/// <summary>
/// Cross-platform memory and storage information service.
/// Detects RAM capacity, type, speed and primary storage type/capacity.
/// </summary>
public class MemoryStorageService : IMemoryStorageService
{
    public async Task<MemoryStorageInfo> GetMemoryStorageInfoAsync() =>
        await Task.Run(GetMemoryStorageInfoSync);

    private MemoryStorageInfo GetMemoryStorageInfoSync()
    {
        var info = new MemoryStorageInfo { Platform = RuntimeInformation.OSDescription };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                PopulateWindowsInfo(info);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                PopulateMacOsInfo(info);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                PopulateLinuxInfo(info);
        }
        catch { /* ignore platform errors */ }

        return info;
    }

    // ── Shared helper ──────────────────────────────────────────────────────

    private static string Run(string file, string args, int timeoutMs = 5000)
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
            var stderrTask = p.StandardError.ReadToEndAsync();
            bool exited = p.WaitForExit(timeoutMs);
            if (!exited)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return "";
            }
            stdoutTask.Wait(500);
            return stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "";
        }
        catch
        {
            return "";
        }
    }

    // ── Windows ────────────────────────────────────────────────────────────

    private static void PopulateWindowsInfo(MemoryStorageInfo info)
    {
        PopulateWindowsMemory(info);
        PopulateWindowsStorage(info);
    }

    private static void PopulateWindowsMemory(MemoryStorageInfo info)
    {
        string out_ = Run("powershell",
            "-NoProfile -Command \"" +
            "Get-CimInstance Win32_PhysicalMemory | " +
            "Select-Object Capacity,Speed,MemoryType,SMBIOSMemoryType | " +
            "Format-List\"");

        long totalBytes = 0;
        int? speed = null;
        string? ramType = null;
        int slotsUsed = 0;

        // Parse each memory module block
        foreach (Match m in Regex.Matches(out_, @"Capacity\s*:\s*(\d+)"))
        {
            if (long.TryParse(m.Groups[1].Value, out long cap))
            {
                totalBytes += cap;
                slotsUsed++;
            }
        }

        var speedMatch = Regex.Match(out_, @"Speed\s*:\s*(\d+)");
        if (speedMatch.Success && int.TryParse(speedMatch.Groups[1].Value, out int spd))
            speed = spd;

        // SMBIOSMemoryType: 26 = DDR4, 34 = DDR5, 20 = DDR3
        var typeMatch = Regex.Match(out_, @"SMBIOSMemoryType\s*:\s*(\d+)");
        if (typeMatch.Success && int.TryParse(typeMatch.Groups[1].Value, out int memType))
        {
            ramType = memType switch
            {
                34 => "DDR5",
                26 => "DDR4",
                24 => "DDR3",
                20 => "DDR3",
                _  => null,
            };
        }

        // Total physical memory from OS as fallback
        if (totalBytes == 0)
        {
            string cs = Run("powershell",
                "-NoProfile -Command \"" +
                "Get-CimInstance Win32_ComputerSystem | " +
                "Select-Object TotalPhysicalMemory | Format-List\"");
            var m2 = Regex.Match(cs, @"TotalPhysicalMemory\s*:\s*(\d+)");
            if (m2.Success && long.TryParse(m2.Groups[1].Value, out long total))
                totalBytes = total;
        }

        // Slot count
        string slots = Run("powershell",
            "-NoProfile -Command \"" +
            "(Get-CimInstance Win32_PhysicalMemoryArray).MemoryDevices\"");
        if (int.TryParse(slots.Trim(), out int slotCount) && slotCount > 0)
            info.RamSlots = slotCount;

        if (totalBytes > 0) info.TotalRamBytes = totalBytes;
        if (speed.HasValue)  info.RamSpeedMhz  = speed.Value;
        if (ramType != null) info.RamType       = ramType;
        if (slotsUsed > 0)   info.RamSlotsUsed  = slotsUsed;
    }

    private static void PopulateWindowsStorage(MemoryStorageInfo info)
    {
        // Get physical disks with bus type (NVMe, SATA, etc.)
        string out_ = Run("powershell",
            "-NoProfile -Command \"" +
            "Get-PhysicalDisk | " +
            "Select-Object FriendlyName,Size,BusType,MediaType | " +
            "Format-List\"");

        long primarySize   = 0;
        long totalStorage  = 0;
        string? primaryType  = null;
        string? primaryModel = null;

        // Parse disk blocks
        var blocks = Regex.Split(out_, @"\r?\n\r?\n");
        foreach (string block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;

            var sizeMatch = Regex.Match(block, @"Size\s*:\s*(\d+)");
            if (!sizeMatch.Success) continue;
            if (!long.TryParse(sizeMatch.Groups[1].Value, out long size)) continue;

            totalStorage += size;

            var busMatch   = Regex.Match(block, @"BusType\s*:\s*(\w+)");
            var mediaMatch = Regex.Match(block, @"MediaType\s*:\s*(.+)");
            var nameMatch  = Regex.Match(block, @"FriendlyName\s*:\s*(.+)");

            string bus   = busMatch.Success   ? busMatch.Groups[1].Value.Trim()   : "";
            string media = mediaMatch.Success ? mediaMatch.Groups[1].Value.Trim() : "";
            string name  = nameMatch.Success  ? nameMatch.Groups[1].Value.Trim()  : "";

            string diskType = ClassifyWindowsDisk(bus, media);

            // Take the largest disk as "primary" (typically C:\ drive)
            if (size > primarySize)
            {
                primarySize  = size;
                primaryType  = diskType;
                primaryModel = name;
            }
        }

        if (primarySize > 0)   info.PrimaryStorageBytes = primarySize;
        if (totalStorage > 0)  info.TotalStorageBytes   = totalStorage;
        if (primaryType != null) info.StorageType        = primaryType;
        if (primaryModel != null) info.StorageModel      = primaryModel;
    }

    private static string ClassifyWindowsDisk(string bus, string media)
    {
        if (bus.Equals("NVMe", StringComparison.OrdinalIgnoreCase))
            return "NVMe SSD";
        if (bus.Equals("SATA", StringComparison.OrdinalIgnoreCase))
        {
            if (media.Contains("SSD", StringComparison.OrdinalIgnoreCase))
                return "SATA SSD";
            return media.Contains("HDD", StringComparison.OrdinalIgnoreCase) ? "HDD" : "SATA SSD";
        }
        if (media.Contains("SSD", StringComparison.OrdinalIgnoreCase)) return "SSD";
        if (media.Contains("HDD", StringComparison.OrdinalIgnoreCase)) return "HDD";
        return "Unknown";
    }

    // ── macOS ──────────────────────────────────────────────────────────────

    private static void PopulateMacOsInfo(MemoryStorageInfo info)
    {
        PopulateMacOsMemory(info);
        PopulateMacOsStorage(info);
    }

    private static void PopulateMacOsMemory(MemoryStorageInfo info)
    {
        // Total physical memory
        string memBytes = Run("sysctl", "-n hw.memsize").Trim();
        if (long.TryParse(memBytes, out long bytes) && bytes > 0)
            info.TotalRamBytes = bytes;

        // Memory details from system_profiler
        string sp = Run("system_profiler", "SPMemoryDataType");

        // Apple Silicon: unified memory — no DIMM info
        if (string.IsNullOrWhiteSpace(sp) || sp.Contains("No memory slots", StringComparison.OrdinalIgnoreCase))
        {
            info.RamType = "Unified Memory (LPDDR5)";
            return;
        }

        var typeMatch  = Regex.Match(sp, @"Type:\s+(.+)");
        var speedMatch = Regex.Match(sp, @"Speed:\s+(\d+)\s*MHz");

        if (typeMatch.Success)  info.RamType    = typeMatch.Groups[1].Value.Trim();
        if (speedMatch.Success && int.TryParse(speedMatch.Groups[1].Value, out int mhz))
            info.RamSpeedMhz = mhz;

        // Slot count
        int slotsUsed = Regex.Matches(sp, @"Size:\s+\d+").Count;
        if (slotsUsed > 0) info.RamSlotsUsed = slotsUsed;
    }

    private static void PopulateMacOsStorage(MemoryStorageInfo info)
    {
        string sp = Run("system_profiler", "SPStorageDataType");

        long totalBytes  = 0;
        long primarySize = 0;
        string? primaryType  = null;
        string? primaryModel = null;

        // Each volume block starts with a name and contains capacity
        var capacityMatches = Regex.Matches(sp, @"Capacity:\s*([\d,]+)\s*bytes");
        var typeMatches     = Regex.Matches(sp, @"Medium Type:\s*(.+)");
        var protocolMatches = Regex.Matches(sp, @"Protocol:\s*(.+)");
        var nameMatches     = Regex.Matches(sp, @"Drive:\s*(.+)");

        for (int i = 0; i < capacityMatches.Count; i++)
        {
            string capStr = capacityMatches[i].Groups[1].Value.Replace(",", "");
            if (!long.TryParse(capStr, out long cap)) continue;

            totalBytes += cap;

            string protocol = protocolMatches.Count > i
                ? protocolMatches[i].Groups[1].Value.Trim() : "";
            string medium   = typeMatches.Count > i
                ? typeMatches[i].Groups[1].Value.Trim() : "";
            string driveName = nameMatches.Count > i
                ? nameMatches[i].Groups[1].Value.Trim() : "";

            string diskType = ClassifyMacDisk(protocol, medium);

            if (cap > primarySize)
            {
                primarySize  = cap;
                primaryType  = diskType;
                primaryModel = driveName;
            }
        }

        // diskutil list as fallback
        if (totalBytes == 0)
        {
            string du = Run("diskutil", "list");
            var m = Regex.Match(du, @"(\d+\.?\d*)\s*(GB|TB)\s+\(virtual\)");
            if (!m.Success)
                m = Regex.Match(du, @"(\d+\.?\d*)\s*(GB|TB)");
            if (m.Success && double.TryParse(m.Groups[1].Value, out double sz))
            {
                double mult = m.Groups[2].Value == "TB" ? 1e12 : 1e9;
                totalBytes   = (long)(sz * mult);
                primarySize  = totalBytes;
                primaryType ??= "NVMe SSD";   // Apple uses NVMe on all modern Macs
            }
        }

        if (primarySize > 0)    info.PrimaryStorageBytes = primarySize;
        if (totalBytes > 0)     info.TotalStorageBytes   = totalBytes;
        if (primaryType != null)  info.StorageType        = primaryType;
        if (primaryModel != null) info.StorageModel       = primaryModel;
    }

    private static string ClassifyMacDisk(string protocol, string medium)
    {
        if (protocol.Contains("NVMe", StringComparison.OrdinalIgnoreCase))  return "NVMe SSD";
        if (protocol.Contains("SATA", StringComparison.OrdinalIgnoreCase))
            return medium.Contains("Solid", StringComparison.OrdinalIgnoreCase) ? "SATA SSD" : "HDD";
        if (medium.Contains("Solid", StringComparison.OrdinalIgnoreCase))    return "SSD";
        return "NVMe SSD";  // Modern Macs default
    }

    // ── Linux ──────────────────────────────────────────────────────────────

    private static void PopulateLinuxInfo(MemoryStorageInfo info)
    {
        PopulateLinuxMemory(info);
        PopulateLinuxStorage(info);
    }

    private static void PopulateLinuxMemory(MemoryStorageInfo info)
    {
        // /proc/meminfo
        string meminfo = "";
        try { meminfo = File.ReadAllText("/proc/meminfo"); } catch { }

        var m = Regex.Match(meminfo, @"MemTotal:\s+(\d+)\s+kB");
        if (m.Success && long.TryParse(m.Groups[1].Value, out long kb))
            info.TotalRamBytes = kb * 1024L;

        // dmidecode (may not be available without root)
        string dmi = Run("dmidecode", "--type 17");
        if (!string.IsNullOrWhiteSpace(dmi))
        {
            var typeMatch  = Regex.Match(dmi, @"Type:\s+(\w+)");
            var speedMatch = Regex.Match(dmi, @"Speed:\s+(\d+)\s*MT");
            if (typeMatch.Success)  info.RamType    = typeMatch.Groups[1].Value.Trim();
            if (speedMatch.Success && int.TryParse(speedMatch.Groups[1].Value, out int speed))
                info.RamSpeedMhz = speed;

            info.RamSlotsUsed = Regex.Matches(dmi, @"Size:\s+\d+ MB").Count;
            info.RamSlots     = Regex.Matches(dmi, @"Memory Device").Count;
        }
    }

    private static void PopulateLinuxStorage(MemoryStorageInfo info)
    {
        // lsblk: NAME, SIZE, TYPE, ROTA, TRAN
        string lsblk = Run("lsblk", "-bdo NAME,SIZE,TYPE,ROTA,TRAN");
        if (string.IsNullOrWhiteSpace(lsblk)) return;

        long primarySize = 0;
        long totalStorage = 0;
        string? primaryType = null;

        foreach (string line in lsblk.Split('\n').Skip(1))
        {
            var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;
            if (parts[2] != "disk") continue;

            if (!long.TryParse(parts[1], out long size)) continue;

            bool rotational = parts.Length > 3 && parts[3] == "1";
            string tran     = parts.Length > 4 ? parts[4].ToUpperInvariant() : "";

            string diskType;
            if (tran == "NVME")
                diskType = "NVMe SSD";
            else if (rotational)
                diskType = "HDD";
            else
                diskType = "SATA SSD";

            totalStorage += size;
            if (size > primarySize)
            {
                primarySize = size;
                primaryType = diskType;
            }
        }

        if (primarySize > 0)    info.PrimaryStorageBytes = primarySize;
        if (totalStorage > 0)   info.TotalStorageBytes   = totalStorage;
        if (primaryType != null)  info.StorageType        = primaryType;
    }
}
