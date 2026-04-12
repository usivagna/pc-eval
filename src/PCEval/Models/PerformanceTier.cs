namespace PCEval.Models;

/// <summary>
/// Describes a class of CPU/SoC with benchmark scores relative to
/// the Apple M3 Pro baseline (100 = M3 Pro level).
/// </summary>
public class PerformanceTier
{
    public string TierId { get; set; } = "";
    public string Label { get; set; } = "";
    public string Vendor { get; set; } = "";
    public string Arch { get; set; } = "";
    public string[] MatchPatterns { get; set; } = [];

    // Benchmark scores (0–120+ scale, 100 = M3 Pro baseline)
    public int? SingleCore { get; set; }
    public int? MultiCore { get; set; }
    public int? Efficiency { get; set; }
    public int? Sustained { get; set; }
    public int? Igpu { get; set; }
    public int? RealWorld { get; set; }
    public int? Platform { get; set; }

    public int? TypicalTdp { get; set; }
    public bool? ActiveCooling { get; set; }

    public string[] Strengths { get; set; } = [];
    public string[] Weaknesses { get; set; } = [];
}
