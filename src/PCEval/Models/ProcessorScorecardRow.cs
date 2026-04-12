namespace PCEval.Models;

/// <summary>
/// A row in the processor scorecard.
/// </summary>
public class ProcessorScorecardRow
{
    public string Metric { get; set; } = "";
    public string Category { get; set; } = "";
    public string Value { get; set; } = "—";
    public string Target { get; set; } = "—";
    public string Result { get; set; } = "—";
    public Color ResultColor { get; set; } = Colors.Gray;
    public string Note { get; set; } = "";
    public string Badge { get; set; } = "";

    /// <summary>Consumer insight combining badge and note.</summary>
    public string Insight => string.IsNullOrEmpty(Badge)
        ? Note
        : $"{Badge} — {Note}";

    /// <summary>Whether this row is a dimension row (PASS/REVIEW/FAIL) vs a summary row.</summary>
    public bool IsDimensionRow => Result is "PASS" or "REVIEW" or "FAIL";
}
