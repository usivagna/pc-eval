namespace PCEval.Models;

/// <summary>
/// Holds memory and storage information collected from the OS.
/// </summary>
public class MemoryStorageInfo
{
    public string? Platform { get; set; }

    // ── Memory ───────────────────────────────────────────────────────────────
    public long? TotalRamBytes { get; set; }
    public string? RamType { get; set; }       // e.g. DDR5, DDR4, LPDDR5
    public int? RamSpeedMhz { get; set; }
    public int? RamSlots { get; set; }
    public int? RamSlotsUsed { get; set; }

    // ── Storage ──────────────────────────────────────────────────────────────
    public long? PrimaryStorageBytes { get; set; }
    public string? StorageType { get; set; }   // NVMe, SATA SSD, HDD, Unknown
    public string? StorageModel { get; set; }
    public long? TotalStorageBytes { get; set; }

    // ── Derived helpers ──────────────────────────────────────────────────────
    public double? TotalRamGb =>
        TotalRamBytes.HasValue ? TotalRamBytes.Value / 1_073_741_824.0 : null;

    public double? PrimaryStorageGb =>
        PrimaryStorageBytes.HasValue ? PrimaryStorageBytes.Value / 1_073_741_824.0 : null;

    public double? TotalStorageGb =>
        TotalStorageBytes.HasValue ? TotalStorageBytes.Value / 1_073_741_824.0 : null;
}
