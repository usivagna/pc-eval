namespace PCEval.Models;

/// <summary>
/// Holds all display information collected from the OS and EDID.
/// All values are self-reported and should be treated as indicative.
/// </summary>
public class DisplayInfo
{
    // ── EDID identity ──────────────────────────────────────────────────────
    public string? ManufacturerId { get; set; }
    public string? ManufacturerName { get; set; }
    public int? ProductCode { get; set; }
    public string? SerialNumber { get; set; }
    public int? ManufactureWeek { get; set; }
    public int? ManufactureYear { get; set; }
    public string? MonitorName { get; set; }

    // ── Physical ───────────────────────────────────────────────────────────
    public double? DiagonalInches { get; set; }
    public string? PanelType { get; set; }

    // ── Resolution & refresh ──────────────────────────────────────────────
    public int? ResolutionWidth { get; set; }
    public int? ResolutionHeight { get; set; }
    public double? RefreshRate { get; set; }
    public bool? AdaptiveSync { get; set; }
    public string? AdaptiveSyncRange { get; set; }
    public int? MinRefreshHz { get; set; }
    public int? MaxRefreshHz { get; set; }

    // ── Colour gamut ──────────────────────────────────────────────────────
    public double? ColorRx { get; set; }
    public double? ColorRy { get; set; }
    public double? ColorGx { get; set; }
    public double? ColorGy { get; set; }
    public double? ColorBx { get; set; }
    public double? ColorBy { get; set; }
    public double? ColorWx { get; set; }
    public double? ColorWy { get; set; }
    public double? GamutSrgbPct { get; set; }
    public double? GamutP3Pct { get; set; }
    public double? GamutAdobeRgbPct { get; set; }

    // ── HDR ───────────────────────────────────────────────────────────────
    public string? HdrTier { get; set; }
    public bool? HdrSupported { get; set; }
    public int? MaxLuminanceNits { get; set; }

    // ── Platform extras ───────────────────────────────────────────────────
    public string? IccProfileName { get; set; }
    public string? IccProfilePath { get; set; }
    public bool? IsRetina { get; set; }
    public bool? TrueTone { get; set; }

    /// <summary>A human-readable display name for UI selectors.</summary>
    public string DisplayName =>
        MonitorName ?? ManufacturerName ?? ManufacturerId ?? "Unknown Display";
}
