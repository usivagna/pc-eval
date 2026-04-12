namespace PCEval.Models;

/// <summary>
/// A row in the display scorecard.
/// </summary>
public class DisplayScorecardRow
{
    public string Metric { get; set; } = "";
    public string Value { get; set; } = "—";
    public string Target { get; set; } = "—";
    public string Result { get; set; } = "—";
    public Color ResultColor { get; set; } = Colors.Gray;
}
