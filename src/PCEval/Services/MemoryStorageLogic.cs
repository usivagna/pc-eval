using PCEval.Models;

namespace PCEval.Services;

/// <summary>
/// Memory and storage scorecard logic.
/// Scores PC memory and storage against the Apple M3 Pro baseline.
/// </summary>
public static class MemoryStorageLogic
{
    // ── Apple M3 Pro reference values ────────────────────────────────────────
    //   RAM    : 18 GB unified memory (LPDDR5 class, ~150 GB/s bandwidth)
    //   Storage: 512 GB custom NVMe SSD (~7.4 GB/s sequential read)
    //   Neither RAM nor storage is upgradeable after purchase on Apple Silicon.

    private const double ReferenceRamGb              = 18.0;
    private const double ReferenceStorageGb          = 512.0;
    private const double ReferenceStorageReadGbps    = 7.4;   // GB/s sequential read

    // Estimated sequential read speeds by category
    private const double NvmeGen5EstReadGbps  = 12.0;
    private const double NvmeGen4EstReadGbps  = 6.0;
    private const double NvmeGen3EstReadGbps  = 3.5;
    private const double SataSsdEstReadGbps   = 0.55;
    private const double HddEstReadGbps       = 0.15;

    // ── Pass/Review/Fail thresholds ──────────────────────────────────────────

    private static string RamCapacityResult(double ramGb) =>
        ramGb >= 32 ? "PASS" : ramGb >= 16 ? "REVIEW" : "FAIL";

    private static string RamSpeedResult(string? ramType, int? speedMhz)
    {
        string t = (ramType ?? "").ToUpperInvariant();
        if (t.Contains("DDR5") || t.Contains("LPDDR5"))
            return "PASS";
        if (t.Contains("DDR4") || t.Contains("LPDDR4"))
            return "REVIEW";
        if (t.Contains("DDR3") || t.Contains("LPDDR3"))
            return "FAIL";

        // Fall back to speed
        if (speedMhz.HasValue)
        {
            if (speedMhz.Value >= 4800) return "PASS";
            if (speedMhz.Value >= 2133) return "REVIEW";
            return "FAIL";
        }
        return "REVIEW";
    }

    private static string StorageCapacityResult(double storageGb) =>
        storageGb >= 512 ? "PASS" : storageGb >= 256 ? "REVIEW" : "FAIL";

    private static string StorageTypeResult(string? storageType)
    {
        string t = (storageType ?? "").ToUpperInvariant();
        if (t.Contains("NVME")) return "PASS";
        if (t.Contains("SSD") || t.Contains("SATA")) return "REVIEW";
        return "FAIL";
    }

    private static (string result, double estimatedGbps) StorageSpeedResult(string? storageType)
    {
        string t = (storageType ?? "").ToUpperInvariant();
        if (t.Contains("GEN5") || t.Contains("GEN 5"))
            return ("PASS", NvmeGen5EstReadGbps);
        if (t.Contains("NVME") && (t.Contains("GEN4") || t.Contains("GEN 4")))
            return ("REVIEW", NvmeGen4EstReadGbps);
        if (t.Contains("NVME"))
            return ("REVIEW", NvmeGen4EstReadGbps);   // assume Gen4 for modern NVMe
        if (t.Contains("SSD") || t.Contains("SATA"))
            return ("FAIL", SataSsdEstReadGbps);
        return ("FAIL", HddEstReadGbps);
    }

    // ── Score → grade helpers ────────────────────────────────────────────────

    private static Color ResultColor(string result) => result switch
    {
        "PASS"   => Color.FromArgb("#007a00"),
        "REVIEW" => Color.FromArgb("#b85c00"),
        "FAIL"   => Color.FromArgb("#cc0000"),
        _        => Colors.Gray,
    };

    private static string ScoreLabel(double pct)
    {
        if (pct >= 90) return "A";
        if (pct >= 75) return "B";
        if (pct >= 60) return "C";
        if (pct >= 40) return "D";
        return "F";
    }

    private static Color ScoreColor(double pct)
    {
        if (pct >= 75) return Color.FromArgb("#007a00");
        if (pct >= 50) return Color.FromArgb("#b85c00");
        return Color.FromArgb("#cc0000");
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Build the full scorecard for the given memory/storage snapshot.</summary>
    public static List<MemoryStorageScorecardRow> BuildScorecard(MemoryStorageInfo info)
    {
        var rows = new List<MemoryStorageScorecardRow>();

        // ── 1. RAM Capacity ──────────────────────────────────────────────────
        {
            double ramGb   = info.TotalRamGb ?? 0;
            string result  = ramGb > 0 ? RamCapacityResult(ramGb) : "REVIEW";
            string value   = ramGb > 0 ? $"{ramGb:F1} GB" : "Unknown";
            string note    = result switch
            {
                "PASS"   => "Meets or exceeds the M3 Pro; comfortable for demanding workloads",
                "REVIEW" => "Similar to the M3 Pro base config; sufficient for most tasks",
                _        => "Below the M3 Pro 18 GB baseline; may limit heavy multitasking",
            };
            rows.Add(new MemoryStorageScorecardRow
            {
                Metric      = "RAM Capacity",
                Value       = value,
                Target      = $"≥ 18 GB (M3 Pro base: {ReferenceRamGb} GB)",
                Result      = result,
                ResultColor = ResultColor(result),
                Note        = note,
            });
        }

        // ── 2. RAM Speed / Type ──────────────────────────────────────────────
        {
            string ramType  = info.RamType ?? "";
            int? speedMhz   = info.RamSpeedMhz;
            string result   = RamSpeedResult(ramType, speedMhz);
            string speedStr = speedMhz.HasValue ? $" @ {speedMhz} MHz" : "";
            string value    = !string.IsNullOrEmpty(ramType)
                ? $"{ramType}{speedStr}"
                : speedMhz.HasValue ? $"{speedMhz} MHz" : "Unknown";
            string note = result switch
            {
                "PASS"   => "Modern high-bandwidth RAM; comparable generation to Apple's LPDDR5",
                "REVIEW" => "Capable but older generation than Apple's LPDDR5 unified memory",
                _        => "Legacy RAM; significantly slower than Apple's unified memory",
            };
            rows.Add(new MemoryStorageScorecardRow
            {
                Metric      = "RAM Speed / Type",
                Value       = value,
                Target      = "DDR5 / LPDDR5 (M3 Pro unified memory)",
                Result      = result,
                ResultColor = ResultColor(result),
                Note        = note,
            });
        }

        // ── 3. Storage Capacity ──────────────────────────────────────────────
        {
            double storGb  = info.PrimaryStorageGb ?? info.TotalStorageGb ?? 0;
            string result  = storGb > 0 ? StorageCapacityResult(storGb) : "REVIEW";
            string value   = storGb > 0 ? FormatStorageSize(storGb) : "Unknown";
            string note    = result switch
            {
                "PASS"   => "Meets or exceeds the M3 Pro base storage; room for large projects",
                "REVIEW" => "Slightly below M3 Pro base; adequate for everyday use",
                _        => "Limited storage; consider an external drive for large files",
            };
            rows.Add(new MemoryStorageScorecardRow
            {
                Metric      = "Storage Capacity",
                Value       = value,
                Target      = $"≥ 512 GB (M3 Pro base: {ReferenceStorageGb:F0} GB)",
                Result      = result,
                ResultColor = ResultColor(result),
                Note        = note,
            });
        }

        // ── 4. Storage Type ──────────────────────────────────────────────────
        {
            string? storType = info.StorageType;
            string result    = StorageTypeResult(storType);
            string value     = !string.IsNullOrEmpty(storType) ? storType : "Unknown";
            string note      = result switch
            {
                "PASS"   => "NVMe SSD; matches the class of Apple's custom storage",
                "REVIEW" => "SATA SSD; capable but noticeably slower than Apple's NVMe",
                _        => "Spinning disk or unknown; significantly slower than Apple SSD",
            };
            rows.Add(new MemoryStorageScorecardRow
            {
                Metric      = "Storage Type",
                Value       = value,
                Target      = "NVMe SSD (M3 Pro custom NVMe)",
                Result      = result,
                ResultColor = ResultColor(result),
                Note        = note,
            });
        }

        // ── 5. Estimated Storage Read Speed ──────────────────────────────────
        {
            var (result, estGbps) = StorageSpeedResult(info.StorageType);
            string value          = $"~{estGbps:F1} GB/s (est.)";
            string targetSpeed    = $"~{ReferenceStorageReadGbps} GB/s (M3 Pro)";
            string note           = result switch
            {
                "PASS"   => "Gen 5 NVMe; faster than Apple's M3 Pro SSD",
                "REVIEW" => "Gen 4 NVMe; slightly below Apple's SSD speed in sustained reads",
                _        => "Well below Apple M3 Pro SSD; large-file transfers will be slower",
            };
            rows.Add(new MemoryStorageScorecardRow
            {
                Metric      = "Est. Storage Read Speed",
                Value       = value,
                Target      = targetSpeed,
                Result      = result,
                ResultColor = ResultColor(result),
                Note        = note,
            });
        }

        // ── 6. Upgrade Potential (summary / info row) ─────────────────────────
        {
            bool hasSlotInfo = info.RamSlots.HasValue;
            string upgradeNote;
            if (hasSlotInfo)
            {
                int free = (info.RamSlots ?? 0) - (info.RamSlotsUsed ?? 0);
                upgradeNote = free > 0
                    ? $"RAM: {free} slot(s) free — can upgrade. Storage: additional drives possible."
                    : "RAM: all slots occupied but replaceable. Storage: additional drives possible.";
            }
            else
            {
                upgradeNote = "PC RAM and storage are typically user-upgradeable; " +
                              "Apple Silicon memory and storage are soldered and cannot be upgraded.";
            }
            rows.Add(new MemoryStorageScorecardRow
            {
                Metric      = "PC Upgrade Advantage",
                Value       = "Upgradeable",
                Target      = "Fixed (Apple soldered)",
                Result      = "—",
                ResultColor = Colors.Gray,
                Note        = upgradeNote,
            });
        }

        return rows;
    }

    /// <summary>Compute the overall letter grade from dimension rows.</summary>
    public static (string grade, Color color, string verdict) OverallGrade(
        IEnumerable<MemoryStorageScorecardRow> rows)
    {
        var dimRows = rows.Where(r => r.IsDimensionRow).ToList();
        if (dimRows.Count == 0)
            return ("—", Colors.Gray, "No scorecard data available");

        double score = dimRows.Average(r => r.Result switch
        {
            "PASS"   => 100.0,
            "REVIEW" => 65.0,
            _        => 30.0,
        });

        string grade   = ScoreLabel(score);
        Color color    = ScoreColor(score);
        string verdict = score switch
        {
            >= 85 => "Memory and storage match or exceed the Apple M3 Pro baseline",
            >= 65 => "Competitive memory and storage; minor gaps vs the M3 Pro",
            _     => "Memory or storage falls noticeably behind the M3 Pro baseline",
        };
        return (grade, color, verdict);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string FormatStorageSize(double gb)
    {
        if (gb >= 1024)
            return $"{gb / 1024:F1} TB";
        return $"{gb:F0} GB";
    }
}
