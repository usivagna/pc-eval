namespace PCEval.Models;

/// <summary>
/// Memory, storage, and graphics info collected from the OS — used together
/// with <see cref="ProcessorInfo"/> and <see cref="DisplayInfo"/> to score the
/// PC against the Mac lineup.
/// </summary>
public class SystemInfo
{
    // ── Memory (RAM) ──────────────────────────────────────────────────────
    public double? TotalRamGb { get; set; }
    /// <summary>"DDR4", "DDR5", "LPDDR5", "LPDDR5X", etc.</summary>
    public string? RamType { get; set; }
    public int? RamSpeedMhz { get; set; }

    // ── Storage ───────────────────────────────────────────────────────────
    /// <summary>System / OS drive size in GB.</summary>
    public double? PrimaryStorageGb { get; set; }
    /// <summary>"NVMe SSD", "SSD", "HDD".</summary>
    public string? PrimaryStorageType { get; set; }
    /// <summary>Sum across all fixed drives.</summary>
    public double? TotalStorageGb { get; set; }

    // ── Graphics ──────────────────────────────────────────────────────────
    /// <summary>Friendly name of the primary GPU (e.g. "NVIDIA GeForce RTX 4070").</summary>
    public string? PrimaryGpuName { get; set; }
    /// <summary>Dedicated VRAM in GB (when available).</summary>
    public double? PrimaryGpuVramGb { get; set; }
    /// <summary>True when the primary GPU is a discrete (non-integrated) card.</summary>
    public bool? IsDiscreteGpu { get; set; }
}
