using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using PCEval.Models;

namespace PCEval.Services;

/// <summary>
/// Cross-platform processor information service.
/// Ported from processor_info.py.
/// </summary>
public class ProcessorService : IProcessorService
{
    public async Task<ProcessorInfo> GetProcessorInfoAsync() =>
        await Task.Run(GetProcessorInfoSync);

    private ProcessorInfo GetProcessorInfoSync()
    {
        var info = new ProcessorInfo { Platform = RuntimeInformation.OSDescription };

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

        // Infer vendor and architecture from model string
        string model    = info.CpuModel ?? "";
        string vendorId = "";
        if (!string.IsNullOrEmpty(model))
        {
            info.Vendor       ??= ProcessorLogic.InferVendor(model, vendorId);
            info.Architecture ??= ProcessorLogic.InferArch(model, vendorId);
            info.Tier         = ProcessorLogic.MatchTier(model);
        }

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
            // Read stdout/stderr asynchronously to prevent deadlock when the
            // stderr buffer fills up.  Enforce the timeout on process exit and
            // kill the process if it exceeds the limit.
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

    private static void PopulateWindowsInfo(ProcessorInfo info)
    {
        string out_ = Run("powershell",
            "-NoProfile -Command \"Get-CimInstance Win32_Processor | " +
            "Select-Object Name,NumberOfCores,NumberOfLogicalProcessors," +
            "MaxClockSpeed,Manufacturer,Caption | Format-List\"");

        Match m;
        m = Regex.Match(out_, @"Name\s*:\s*(.+)");
        if (m.Success) info.CpuModel = m.Groups[1].Value.Trim();

        m = Regex.Match(out_, @"NumberOfCores\s*:\s*(\d+)");
        if (m.Success) info.TotalCores = int.Parse(m.Groups[1].Value);

        m = Regex.Match(out_, @"NumberOfLogicalProcessors\s*:\s*(\d+)");
        if (m.Success) info.TotalThreads = int.Parse(m.Groups[1].Value);

        m = Regex.Match(out_, @"MaxClockSpeed\s*:\s*(\d+)");
        if (m.Success) info.MaxFreqMhz = int.Parse(m.Groups[1].Value);

        m = Regex.Match(out_, @"Manufacturer\s*:\s*(.+)");
        if (m.Success)
        {
            string mfr = m.Groups[1].Value.Trim();
            info.Vendor ??= ProcessorLogic.InferVendor(info.CpuModel ?? "", mfr);
        }
    }

    // ── macOS ──────────────────────────────────────────────────────────────

    private static void PopulateMacOsInfo(ProcessorInfo info)
    {
        // Brand string (Intel)
        string brand = Run("sysctl", "-n machdep.cpu.brand_string").Trim();
        if (!string.IsNullOrEmpty(brand))
            info.CpuModel = brand;

        // Apple Silicon chip name from system_profiler
        if (string.IsNullOrEmpty(info.CpuModel))
        {
            string sp = Run("system_profiler", "SPHardwareDataType");
            var m = Regex.Match(sp, @"Chip:\s+(.+)");
            if (m.Success) info.CpuModel = m.Groups[1].Value.Trim();
        }

        // Core counts
        string pCores = Run("sysctl", "-n hw.perflevel0.logicalcpu").Trim();
        string eCores = Run("sysctl", "-n hw.perflevel1.logicalcpu").Trim();
        string total  = Run("sysctl", "-n hw.logicalcpu").Trim();
        if (int.TryParse(pCores, out int p)) info.PerformanceCores = p;
        if (int.TryParse(eCores, out int e)) info.EfficiencyCores  = e;
        if (int.TryParse(total,  out int t)) info.TotalThreads     = t;

        // CPU frequency
        string freqHz = Run("sysctl", "-n hw.cpufrequency_max").Trim();
        if (long.TryParse(freqHz, out long hz) && hz > 0)
            info.MaxFreqMhz = hz / 1_000_000.0;

        // Total cores from system_profiler
        string spOut = Run("system_profiler", "SPHardwareDataType");
        var mCores   = Regex.Match(spOut, @"Total Number of Cores:\s+(\d+)");
        if (mCores.Success && int.TryParse(mCores.Groups[1].Value, out int tc))
            info.TotalCores ??= tc;
    }

    // ── Linux ──────────────────────────────────────────────────────────────

    private static void PopulateLinuxInfo(ProcessorInfo info)
    {
        // /proc/cpuinfo
        string cpuinfo = "";
        try { cpuinfo = File.ReadAllText("/proc/cpuinfo"); } catch { }

        var m = Regex.Match(cpuinfo, @"model name\s*:\s*(.+)");
        if (m.Success) info.CpuModel = m.Groups[1].Value.Trim();

        m = Regex.Match(cpuinfo, @"cpu MHz\s*:\s*([\d.]+)");
        if (m.Success && double.TryParse(m.Groups[1].Value, out double curMhz))
            info.CurrentFreqMhz = curMhz;

        m = Regex.Match(cpuinfo, @"cache size\s*:\s*(.+)");
        if (m.Success) info.L3Cache = m.Groups[1].Value.Trim();

        info.TotalThreads = Regex.Matches(cpuinfo, @"processor\s*:").Count;

        // lscpu
        string lscpu = Run("lscpu", "");
        m = Regex.Match(lscpu, @"^Core\(s\) per socket:\s*(\d+)", RegexOptions.Multiline);
        int cpk = m.Success && int.TryParse(m.Groups[1].Value, out int ck) ? ck : 1;

        m = Regex.Match(lscpu, @"^Socket\(s\):\s*(\d+)", RegexOptions.Multiline);
        if (m.Success && int.TryParse(m.Groups[1].Value, out int socks))
            info.TotalCores = socks * cpk;

        m = Regex.Match(lscpu, @"^CPU max MHz:\s*([\d.]+)", RegexOptions.Multiline);
        if (m.Success && double.TryParse(m.Groups[1].Value, out double maxMhz))
            info.MaxFreqMhz = maxMhz;

        m = Regex.Match(lscpu, @"^Architecture:\s*(.+)", RegexOptions.Multiline);
        if (m.Success) info.Architecture = m.Groups[1].Value.Trim();

        m = Regex.Match(lscpu, @"^Model name:\s*(.+)", RegexOptions.Multiline);
        if (m.Success) info.CpuModel ??= m.Groups[1].Value.Trim();
    }
}
