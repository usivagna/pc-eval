namespace PCEval.Models;

/// <summary>
/// Result of comparing the user's PC point-by-point against a single Mac config.
/// </summary>
public class MacComparisonResult
{
    public MacConfig Mac { get; set; } = new();

    /// <summary>Per-dimension comparison rows (CPU single-core, multi-core, PPI, etc.).</summary>
    public List<MacComparisonRow> Rows { get; set; } = [];

    /// <summary>Average percent of Mac across all comparable dimensions (0–150+).</summary>
    public double OverallPct { get; set; }

    /// <summary>"Beats", "Matches", "Trails", "Mixed".</summary>
    public string Verdict { get; set; } = "";

    public Color VerdictColor { get; set; } = Colors.Gray;

    /// <summary>Short verdict line for the card header.</summary>
    public string Summary { get; set; } = "";
}

/// <summary>
/// One point-by-point row inside a <see cref="MacComparisonResult"/>.
/// </summary>
public class MacComparisonRow
{
    /// <summary>"CPU Single-Core", "Pixel Density", "Refresh Rate", etc.</summary>
    public string Metric { get; set; } = "";

    /// <summary>What the user's PC reports (e.g. "108", "157 PPI", "144 Hz").</summary>
    public string YourValue { get; set; } = "";

    /// <summary>What this Mac config offers (e.g. "M4 Pro: 110", "254 PPI", "120 Hz ProMotion").</summary>
    public string MacValue { get; set; } = "";

    /// <summary>"BETTER", "MATCH", "WORSE", "—" (unknown).</summary>
    public string Result { get; set; } = "";

    public Color ResultColor { get; set; } = Colors.Gray;

    /// <summary>Optional human-readable note.</summary>
    public string Note { get; set; } = "";
}
