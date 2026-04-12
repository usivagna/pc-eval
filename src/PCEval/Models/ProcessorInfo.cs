namespace PCEval.Models;

/// <summary>
/// Holds processor information collected from the OS.
/// </summary>
public class ProcessorInfo
{
    public string? Platform { get; set; }
    public string? CpuModel { get; set; }
    public string? Vendor { get; set; }
    public string? Architecture { get; set; }
    public int? TotalCores { get; set; }
    public int? TotalThreads { get; set; }
    public int? PerformanceCores { get; set; }
    public int? EfficiencyCores { get; set; }
    public double? MaxFreqMhz { get; set; }
    public double? CurrentFreqMhz { get; set; }
    public string? L3Cache { get; set; }
    public PerformanceTier? Tier { get; set; }
}
