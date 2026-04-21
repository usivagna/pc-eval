namespace PCEval.Models;

/// <summary>
/// A row in the memory and storage scorecard.
/// </summary>
public class MemoryStorageScorecardRow
{
    public string Metric { get; set; } = "";
    public string Value { get; set; } = "—";
    public string Target { get; set; } = "—";
    public string Result { get; set; } = "—";
    public Color ResultColor { get; set; } = Colors.Gray;
    public string Note { get; set; } = "";

    /// <summary>Whether this row is a scoreable dimension row (PASS/REVIEW/FAIL).</summary>
    public bool IsDimensionRow => Result is "PASS" or "REVIEW" or "FAIL";
}
