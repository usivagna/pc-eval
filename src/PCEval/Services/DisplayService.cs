using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using PCEval.Models;

namespace PCEval.Services;

/// <summary>
/// Cross-platform display information service.
/// Delegates to platform-specific collectors at runtime.
/// Ported from display_info.py (_get_windows_info_all, _get_macos_info, _get_linux_info,
/// _get_windows_info_all, _get_macos_info_all, _get_linux_info_all).
/// </summary>
public class DisplayService : IDisplayService
{
    public async Task<List<DisplayInfo>> GetAllDisplaysInfoAsync() =>
        await Task.Run(GetAllDisplaysInfoSync);

    private List<DisplayInfo> GetAllDisplaysInfoSync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsDisplaysInfo();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacOsDisplaysInfo();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetLinuxDisplaysInfo();
        }
        catch { /* fall through to default */ }

        return [new DisplayInfo()];
    }

    // ── Shared helper ──────────────────────────────────────────────────────

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

    private List<DisplayInfo> GetWindowsDisplaysInfo()
    {
        var displays = new List<DisplayInfo>();

        // Per-monitor resolution via Win32_VideoController
        string vcJson = Run("powershell",
            "-NoProfile -Command \"Get-WmiObject Win32_VideoController | " +
            "Select-Object CurrentHorizontalResolution,CurrentVerticalResolution," +
            "CurrentRefreshRate | ConvertTo-Json\"");

        var vcList = new List<JsonElement>();
        if (!string.IsNullOrWhiteSpace(vcJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(vcJson.Trim());
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var el in doc.RootElement.EnumerateArray())
                        vcList.Add(el.Clone());
                else
                    vcList.Add(doc.RootElement.Clone());
            }
            catch { /* ignore JSON errors */ }
        }

        // Per-monitor EDID from registry
        string edidScript =
            "$mons = Get-PnpDevice -Class Monitor -Status OK -ErrorAction SilentlyContinue; " +
            "foreach ($mon in $mons) { " +
            "  $path = 'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\' + $mon.InstanceId + '\\Device Parameters'; " +
            "  $item = Get-ItemProperty $path -Name EDID -ErrorAction SilentlyContinue; " +
            "  if ($item) { [BitConverter]::ToString($item.EDID).Replace('-','') } else { '' } }";
        string edidOut = Run("powershell", $"-NoProfile -Command \"{edidScript}\"");

        var edidLines = (edidOut ?? "")
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToList();

        int monitorCount = Math.Max(vcList.Count, Math.Max(edidLines.Count, 1));

        for (int i = 0; i < monitorCount; i++)
        {
            var info = new DisplayInfo();

            // Resolution
            if (i < vcList.Count)
            {
                var vc = vcList[i];
                TryGetInt(vc, "CurrentHorizontalResolution", out int w);
                TryGetInt(vc, "CurrentVerticalResolution",   out int h);
                TryGetDouble(vc, "CurrentRefreshRate",       out double rr);
                if (w > 0) info.ResolutionWidth  = w;
                if (h > 0) info.ResolutionHeight = h;
                if (rr > 0) info.RefreshRate = rr;
            }

            // EDID
            if (i < edidLines.Count && edidLines[i].Length >= 256)
            {
                try
                {
                    byte[] edidBytes = Convert.FromHexString(edidLines[i]);
                    var edidInfo     = DisplayLogic.ParseEdid(edidBytes);
                    MergeEdid(info, edidInfo);
                }
                catch { /* skip malformed EDID */ }
            }

            // Prefer EDID max refresh over WMI
            if (info.MaxRefreshHz.HasValue &&
                (info.RefreshRate ?? 0) < info.MaxRefreshHz.Value)
                info.RefreshRate = info.MaxRefreshHz.Value;

            // HDR
            if (info.HdrTier is null && info.HdrSupported == true)
            {
                info.HdrTier = "HDR" +
                    (info.MaxLuminanceNits.HasValue ? $" (~{info.MaxLuminanceNits} cd/m²)" : "");
            }

            // Adaptive sync
            if (info.MinRefreshHz.HasValue && info.MaxRefreshHz.HasValue &&
                info.MaxRefreshHz > info.MinRefreshHz)
            {
                info.AdaptiveSync      = true;
                info.AdaptiveSyncRange = $"{info.MinRefreshHz}–{info.MaxRefreshHz} Hz";
            }

            // ICC profile
            if (i == 0) // primary display only for ICC
            {
                string iccOut = Run("powershell",
                    "-NoProfile -Command \"Get-ChildItem " +
                    "'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Class\\" +
                    "{4d36e96e-e325-11ce-bfc1-08002be10318}\\*' " +
                    "-ErrorAction SilentlyContinue | ForEach-Object { " +
                    "$p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue; " +
                    "if ($p.ICMProfile) { $p.ICMProfile -join ', ' } " +
                    "} | Select-Object -First 1\"");
                if (!string.IsNullOrWhiteSpace(iccOut))
                {
                    info.IccProfileName = iccOut.Trim();
                }
            }

            displays.Add(info);
        }

        return displays.Count > 0 ? displays : [new DisplayInfo()];
    }

    // ── macOS ──────────────────────────────────────────────────────────────

    private List<DisplayInfo> GetMacOsDisplaysInfo()
    {
        var displays = new List<DisplayInfo>();

        string raw = Run("system_profiler", "SPDisplaysDataType -json");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw.Trim());
                var root = doc.RootElement;
                if (root.TryGetProperty("SPDisplaysDataType", out var gpuArr))
                {
                    foreach (var gpu in gpuArr.EnumerateArray())
                    {
                        if (!gpu.TryGetProperty("spdisplays_ndrvs", out var dispArr)) continue;
                        foreach (var disp in dispArr.EnumerateArray())
                        {
                            var info = new DisplayInfo();

                            // Resolution
                            if (disp.TryGetProperty("spdisplays_pixelresolution", out var pxRes))
                            {
                                string px = pxRes.GetString() ?? "";
                                var parts = px.Replace("Retina", "")
                                             .Split('x', StringSplitOptions.TrimEntries);
                                if (parts.Length == 2 &&
                                    int.TryParse(parts[0], out int w) &&
                                    int.TryParse(parts[1].Split(' ')[0], out int h))
                                {
                                    info.ResolutionWidth  = w;
                                    info.ResolutionHeight = h;
                                }
                                info.IsRetina = px.Contains("Retina", StringComparison.OrdinalIgnoreCase);
                            }

                            // Refresh rate
                            if (disp.TryGetProperty("spdisplays_resolution", out var resEl))
                            {
                                string res = resEl.GetString() ?? "";
                                var m = Regex.Match(res, @"@\s*([\d.]+)\s*Hz");
                                if (m.Success && double.TryParse(m.Groups[1].Value, out double hz))
                                    info.RefreshRate = hz;
                            }

                            // HDR
                            if (disp.TryGetProperty("spdisplays_hdr", out var hdrEl))
                            {
                                string hdr = hdrEl.GetString() ?? "";
                                if (!string.IsNullOrEmpty(hdr)) info.HdrTier = hdr;
                            }

                            // True Tone
                            if (disp.TryGetProperty("spdisplays_truetone", out var ttEl))
                            {
                                string tt = ttEl.GetString() ?? "";
                                info.TrueTone = tt.Contains("on", StringComparison.OrdinalIgnoreCase) ||
                                               tt.Contains("yes", StringComparison.OrdinalIgnoreCase);
                            }

                            displays.Add(info);
                        }
                    }
                }
            }
            catch { /* ignore JSON errors */ }
        }

        // EDID via ioreg (primary display only for simplicity)
        if (displays.Count > 0)
        {
            string ioregOut = Run("ioreg", "-l -d 0 -r -c IODisplayConnect");
            var m = Regex.Match(ioregOut, @"""IODisplayEDID""\s*=\s*<([0-9a-fA-F]+)>");
            if (m.Success)
            {
                try
                {
                    byte[] edidBytes = Convert.FromHexString(m.Groups[1].Value);
                    var edidInfo     = DisplayLogic.ParseEdid(edidBytes);
                    MergeEdid(displays[0], edidInfo);
                }
                catch { /* ignore */ }
            }
        }

        // Adaptive sync
        foreach (var info in displays)
        {
            if (info.MinRefreshHz.HasValue && info.MaxRefreshHz.HasValue &&
                info.MinRefreshHz != info.MaxRefreshHz)
            {
                info.AdaptiveSync      = true;
                info.AdaptiveSyncRange = $"{info.MinRefreshHz}–{info.MaxRefreshHz} Hz";
            }
        }

        // ICC profile (per-user ColorSync)
        if (displays.Count > 0)
        {
            string csDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "ColorSync", "Profiles");
            if (Directory.Exists(csDir))
            {
                string? icc = Directory.EnumerateFiles(csDir, "*.icc").FirstOrDefault();
                if (icc is not null)
                {
                    displays[0].IccProfileName = Path.GetFileName(icc);
                    displays[0].IccProfilePath = icc;
                }
            }
        }

        return displays.Count > 0 ? displays : [new DisplayInfo()];
    }

    // ── Linux ──────────────────────────────────────────────────────────────

    private List<DisplayInfo> GetLinuxDisplaysInfo()
    {
        var displays = new List<DisplayInfo>();

        string xrandrOut = Run("xrandr", "--verbose");
        if (!string.IsNullOrWhiteSpace(xrandrOut))
        {
            var lines = xrandrOut.Split('\n');
            DisplayInfo? current = null;
            foreach (string line in lines)
            {
                // "HDMI-1 connected primary 1920x1080+0+0 …"
                var m = Regex.Match(line, @"^\S+\s+connected(?:\s+primary)?\s+(\d+)x(\d+)");
                if (m.Success)
                {
                    current = new DisplayInfo
                    {
                        ResolutionWidth  = int.Parse(m.Groups[1].Value),
                        ResolutionHeight = int.Parse(m.Groups[2].Value),
                    };
                    displays.Add(current);
                }
                if (current is not null)
                {
                    var rr = Regex.Match(line, @"([\d.]+)\*");
                    if (rr.Success && double.TryParse(rr.Groups[1].Value, out double hz))
                        current.RefreshRate = hz;
                }
            }

            if (xrandrOut.Contains("vrr", StringComparison.OrdinalIgnoreCase) ||
                xrandrOut.Contains("freesync", StringComparison.OrdinalIgnoreCase))
                foreach (var d in displays)
                    d.AdaptiveSync = true;
        }

        // EDID from sysfs
        string drmBase = "/sys/class/drm";
        if (Directory.Exists(drmBase) && displays.Count > 0)
        {
            int edidIdx = 0;
            foreach (string connector in Directory.EnumerateDirectories(drmBase))
            {
                string edidPath = Path.Combine(connector, "edid");
                if (!File.Exists(edidPath)) continue;
                try
                {
                    byte[] edidBytes = File.ReadAllBytes(edidPath);
                    if (edidBytes.Length >= 128)
                    {
                        var edidInfo = DisplayLogic.ParseEdid(edidBytes);
                        if (edidIdx < displays.Count)
                            MergeEdid(displays[edidIdx], edidInfo);
                        edidIdx++;
                    }
                }
                catch { /* skip */ }
            }
        }

        // HDR sysfs flag
        if (displays.Count > 0 &&
            File.Exists("/sys/class/drm/card0-HDMI-A-1/hdr_output_metadata"))
            displays[0].HdrTier ??= "HDR (OS-signalled)";

        return displays.Count > 0 ? displays : [new DisplayInfo()];
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void MergeEdid(DisplayInfo target, DisplayInfo edid)
    {
        target.ManufacturerId   ??= edid.ManufacturerId;
        target.ManufacturerName ??= edid.ManufacturerName;
        target.ProductCode      ??= edid.ProductCode;
        target.SerialNumber     ??= edid.SerialNumber;
        target.ManufactureWeek  ??= edid.ManufactureWeek;
        target.ManufactureYear  ??= edid.ManufactureYear;
        target.MonitorName      ??= edid.MonitorName;
        target.DiagonalInches   ??= edid.DiagonalInches;
        target.PanelType        ??= edid.PanelType;
        target.MinRefreshHz     ??= edid.MinRefreshHz;
        target.MaxRefreshHz     ??= edid.MaxRefreshHz;
        target.ColorRx          ??= edid.ColorRx;
        target.ColorRy          ??= edid.ColorRy;
        target.ColorGx          ??= edid.ColorGx;
        target.ColorGy          ??= edid.ColorGy;
        target.ColorBx          ??= edid.ColorBx;
        target.ColorBy          ??= edid.ColorBy;
        target.ColorWx          ??= edid.ColorWx;
        target.ColorWy          ??= edid.ColorWy;
        target.GamutSrgbPct     ??= edid.GamutSrgbPct;
        target.GamutP3Pct       ??= edid.GamutP3Pct;
        target.GamutAdobeRgbPct ??= edid.GamutAdobeRgbPct;
        target.HdrTier          ??= edid.HdrTier;
        target.HdrSupported     ??= edid.HdrSupported;
        target.MaxLuminanceNits ??= edid.MaxLuminanceNits;
    }

    private static bool TryGetInt(JsonElement el, string prop, out int value)
    {
        value = 0;
        if (el.TryGetProperty(prop, out var v))
            return v.TryGetInt32(out value);
        return false;
    }

    private static bool TryGetDouble(JsonElement el, string prop, out double value)
    {
        value = 0;
        if (el.TryGetProperty(prop, out var v))
            return v.TryGetDouble(out value);
        return false;
    }
}
