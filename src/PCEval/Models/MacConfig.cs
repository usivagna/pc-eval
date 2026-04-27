namespace PCEval.Models;

/// <summary>
/// One configuration in the current Apple Mac lineup, used as a comparison
/// reference point against the user's PC. Display fields are null for headless
/// Macs (Mac mini, Mac Studio, Mac Pro).
/// </summary>
public class MacConfig
{
    /// <summary>Stable identifier (e.g. "mbp14_m4_pro").</summary>
    public string Id { get; set; } = "";

    /// <summary>User-facing name (e.g. "MacBook Pro 14\" — M4 Pro").</summary>
    public string Label { get; set; } = "";

    /// <summary>Form factor — "Laptop", "Desktop", "All-in-one", "Mini", "Workstation".</summary>
    public string FormFactor { get; set; } = "";

    /// <summary>Marketing chip name (e.g. "Apple M4 Pro").</summary>
    public string Chip { get; set; } = "";

    /// <summary>TierId in <see cref="Services.ProcessorLogic.PerformanceTiers"/>.</summary>
    public string ChipTierId { get; set; } = "";

    /// <summary>True when the Mac ships with a built-in display.</summary>
    public bool HasBuiltInDisplay { get; set; }

    public double? DisplayDiagonalInches { get; set; }
    public int? DisplayWidth { get; set; }
    public int? DisplayHeight { get; set; }
    public int? DisplayPpi { get; set; }
    public double? RefreshRateHz { get; set; }
    public bool ProMotion { get; set; }

    /// <summary>P3 gamut coverage percentage (Apple specs typically 99%).</summary>
    public double? P3GamutPct { get; set; }

    public bool HdrSupported { get; set; }

    /// <summary>Peak brightness in nits (HDR peak when applicable).</summary>
    public int? PeakBrightnessNits { get; set; }

    /// <summary>Sustained brightness in nits (SDR / full-screen).</summary>
    public int? SustainedBrightnessNits { get; set; }

    /// <summary>Starting US price in USD.</summary>
    public int? StartingPriceUsd { get; set; }

    // ── Memory (unified) ──────────────────────────────────────────────────
    /// <summary>Base unified-memory configuration in GB.</summary>
    public int? BaseRamGb { get; set; }
    /// <summary>Maximum unified-memory configuration in GB.</summary>
    public int? MaxRamGb { get; set; }
    /// <summary>"Unified LPDDR5X", etc.</summary>
    public string? MemoryType { get; set; }
    /// <summary>Memory bandwidth in GB/s.</summary>
    public int? MemoryBandwidthGbs { get; set; }

    // ── Storage ───────────────────────────────────────────────────────────
    /// <summary>Base SSD size in GB.</summary>
    public int? BaseStorageGb { get; set; }
    /// <summary>Maximum SSD size in GB.</summary>
    public int? MaxStorageGb { get; set; }

    // ── Graphics (Apple GPU is integrated; treated as the primary GPU) ────
    /// <summary>Marketing GPU label (e.g. "10-core GPU").</summary>
    public string? GpuLabel { get; set; }
    /// <summary>FP32 TFLOPS — approximate.</summary>
    public double? GpuTflops { get; set; }

    public string Notes { get; set; } = "";
}
