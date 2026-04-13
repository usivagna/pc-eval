using PCEval.Services;
using Xunit;

namespace PCEval.Tests;

/// <summary>
/// Tests for DisplayLogic — ported equivalent of test_display_info.py tests
/// that cover EDID parsing, gamut coverage, and scorecard logic.
/// </summary>
public class DisplayLogicTests
{
    // ── Manufacturer ID decoding ──────────────────────────────────────────

    [Fact]
    public void DecodeManufacturerId_Apple_ReturnsApp()
    {
        // A=1, P=16, P=16
        // APP encoded: (A-0x40=1)<<10 | (P-0x40=16)<<5 | (P-0x40=16)
        ushort raw = (ushort)((1 << 10) | (16 << 5) | 16);
        Assert.Equal("APP", DisplayLogic.DecodeManufacturerId(raw));
    }

    [Fact]
    public void DecodeManufacturerId_Dell_ReturnsDel()
    {
        // D=4, E=5, L=12
        ushort raw = (ushort)((4 << 10) | (5 << 5) | 12);
        Assert.Equal("DEL", DisplayLogic.DecodeManufacturerId(raw));
    }

    // ── Gamut coverage ────────────────────────────────────────────────────

    [Fact]
    public void CalculateGamutCoverage_IdenticalTriangles_Returns100()
    {
        double? result = DisplayLogic.CalculateGamutCoverage(
            DisplayLogic.SrgbPrimaries,
            DisplayLogic.SrgbPrimaries);
        Assert.NotNull(result);
        Assert.True(Math.Abs(result.Value - 100.0) < 0.01,
            $"Expected ~100%, got {result.Value:F4}%");
    }

    [Fact]
    public void CalculateGamutCoverage_ZeroAreaReference_ReturnsNull()
    {
        // Degenerate reference (all same point)
        var degenerate = new (double x, double y)[] { (0.3, 0.3), (0.3, 0.3), (0.3, 0.3) };
        double? result = DisplayLogic.CalculateGamutCoverage(
            DisplayLogic.SrgbPrimaries, degenerate);
        Assert.Null(result);
    }

    [Fact]
    public void CalculateGamutCoverage_DciP3LargerThanSrgb_ExceededReturns100()
    {
        // DCI-P3 is larger than sRGB, so sRGB is fully contained → coverage ≥ 100% → clamped to 100
        double? result = DisplayLogic.CalculateGamutCoverage(
            DisplayLogic.DciP3Primaries,
            DisplayLogic.SrgbPrimaries);
        Assert.NotNull(result);
        Assert.Equal(100.0, result!.Value, precision: 0);
    }

    [Fact]
    public void CalculateGamutCoverage_SrgbVersusDciP3_LessThan100()
    {
        // sRGB does not cover all of DCI-P3
        double? result = DisplayLogic.CalculateGamutCoverage(
            DisplayLogic.SrgbPrimaries,
            DisplayLogic.DciP3Primaries);
        Assert.NotNull(result);
        Assert.True(result!.Value < 100.0, $"Expected <100%, got {result.Value:F2}%");
        Assert.True(result!.Value > 50.0,  $"Expected >50%, got {result.Value:F2}%");
    }

    [Fact]
    public void CalculateGamutCoverage_DisjointTriangles_ReturnsZero()
    {
        var display   = new (double x, double y)[] { (0.0, 0.0), (0.1, 0.0), (0.05, 0.1) };
        var reference = new (double x, double y)[] { (0.8, 0.8), (0.9, 0.8), (0.85, 0.9) };
        double? result = DisplayLogic.CalculateGamutCoverage(display, reference);
        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value, precision: 5);
    }

    [Fact]
    public void CalculateGamutCoverage_SrgbVsAdobeRgb_ReasonableRange()
    {
        double? result = DisplayLogic.CalculateGamutCoverage(
            DisplayLogic.SrgbPrimaries,
            DisplayLogic.AdobeRgbPrimaries);
        Assert.NotNull(result);
        // sRGB covers roughly 72% of Adobe RGB
        Assert.InRange(result!.Value, 60.0, 85.0);
    }

    // ── Retina PPI helpers ────────────────────────────────────────────────

    [Theory]
    [InlineData(18, 190, 215)]  // 18-inch: ≈191 PPI threshold
    [InlineData(10, 340, 360)]  // 10-inch: ≈344 PPI threshold
    [InlineData(30, 110, 130)]  // 30-inch: ≈115 PPI threshold
    public void RetinaMinPpi_ReasonableRange(double distance, double min, double max)
    {
        double ppi = DisplayLogic.RetinaMinPpi(distance);
        Assert.InRange(ppi, min, max);
    }

    [Fact]
    public void CalculatePpi_KnownDisplay_CorrectResult()
    {
        // 1920×1080 at 27 inches ≈ 81.6 PPI
        double ppi = DisplayLogic.CalculatePpi(1920, 1080, 27.0);
        Assert.InRange(ppi, 81.0, 82.5);
    }

    [Fact]
    public void CalculatePpi_MacBookPro14_HighPpi()
    {
        // 3024×1964 at 14.2 inches ≈ 254 PPI
        double ppi = DisplayLogic.CalculatePpi(3024, 1964, 14.2);
        Assert.InRange(ppi, 245.0, 265.0);
    }

    // ── Score label ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(95, "A")]
    [InlineData(80, "B")]
    [InlineData(65, "C")]
    [InlineData(45, "D")]
    [InlineData(20, "F")]
    public void ScoreLabel_CorrectGrade(double pct, string expected) =>
        Assert.Equal(expected, DisplayLogic.ScoreLabel(pct));

    // ── EDID parsing ──────────────────────────────────────────────────────

    [Fact]
    public void ParseEdid_TooShort_ReturnsEmpty()
    {
        var result = DisplayLogic.ParseEdid(new byte[64]);
        Assert.Null(result.ManufacturerId);
        Assert.Null(result.MonitorName);
    }

    [Fact]
    public void ParseEdid_WrongHeader_ReturnsEmpty()
    {
        var data = new byte[128];
        data[0] = 0x01; // wrong header
        var result = DisplayLogic.ParseEdid(data);
        Assert.Null(result.ManufacturerId);
    }

    [Fact]
    public void ParseEdid_ValidHeader_ParsesManufacturer()
    {
        var data = BuildMinimalEdid();
        // Encode "DEL" in bytes 8-9 (D=4, E=5, L=12)
        ushort mfr = (ushort)((4 << 10) | (5 << 5) | 12);
        data[8] = (byte)(mfr >> 8);
        data[9] = (byte)(mfr & 0xFF);

        var result = DisplayLogic.ParseEdid(data);
        Assert.Equal("DEL", result.ManufacturerId);
        Assert.Equal("Dell", result.ManufacturerName);
    }

    [Fact]
    public void ParseEdid_DigitalDisplayPort_PanelTypeDisplayPort()
    {
        var data = BuildMinimalEdid();
        data[20] = (byte)(0x80 | 0x05); // digital + DisplayPort
        var result = DisplayLogic.ParseEdid(data);
        Assert.Equal("DisplayPort", result.PanelType);
    }

    [Fact]
    public void ParseEdid_AnalogInput_PanelTypeAnalog()
    {
        var data = BuildMinimalEdid();
        data[20] = 0x00; // analog
        var result = DisplayLogic.ParseEdid(data);
        Assert.Equal("Analog (VGA)", result.PanelType);
    }

    [Fact]
    public void ParseEdid_PhysicalSize_ComputesDiagonal()
    {
        var data = BuildMinimalEdid();
        data[21] = 60; // 60 cm wide
        data[22] = 34; // 34 cm tall  → diagonal = √(60²+34²) / 2.54 ≈ 26.9 in
        var result = DisplayLogic.ParseEdid(data);
        Assert.NotNull(result.DiagonalInches);
        Assert.InRange(result.DiagonalInches!.Value, 25.0, 28.0);
    }

    [Fact]
    public void ParseEdid_ManufactureYear_Parsed()
    {
        var data = BuildMinimalEdid();
        data[17] = (byte)(2024 - 1990); // year offset
        var result = DisplayLogic.ParseEdid(data);
        Assert.Equal(2024, result.ManufactureYear);
    }

    [Fact]
    public void ParseEdid_MonitorNameDescriptor_Parsed()
    {
        var data = BuildMinimalEdid();
        // Write a monitor name descriptor at offset 72
        data[72] = 0x00;
        data[73] = 0x00;
        data[74] = 0x00;
        data[75] = 0xFC; // monitor name tag
        data[76] = 0x00;
        var name = System.Text.Encoding.Latin1.GetBytes("Test Monitor  \n");
        Array.Copy(name, 0, data, 77, Math.Min(name.Length, 13));

        var result = DisplayLogic.ParseEdid(data);
        Assert.Equal("Test Monitor", result.MonitorName);
    }

    [Fact]
    public void ParseEdid_RangeLimitsDescriptor_ParsesRefreshRates()
    {
        var data = BuildMinimalEdid();
        // Range limits descriptor at offset 54
        data[54] = 0x00; data[55] = 0x00; data[56] = 0x00;
        data[57] = 0xFD; // range limits tag
        data[58] = 0x00;
        data[59] = 48;   // min refresh 48 Hz
        data[60] = 144;  // max refresh 144 Hz

        var result = DisplayLogic.ParseEdid(data);
        Assert.Equal(48, result.MinRefreshHz);
        Assert.Equal(144, result.MaxRefreshHz);
    }

    // ── BuildScorecard ────────────────────────────────────────────────────

    [Fact]
    public void BuildScorecard_NoDiagonal_PpiRowIsDash()
    {
        var info = new Models.DisplayInfo { ResolutionWidth = 1920, ResolutionHeight = 1080 };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, 1920, 1080, 0.0, 18.0);
        Assert.Equal("—", rows[0].Value); // PPI row
    }

    [Fact]
    public void BuildScorecard_HighDpiDisplay_PassesPpiCheck()
    {
        var info = new Models.DisplayInfo { ResolutionWidth = 3024, ResolutionHeight = 1964 };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, 3024, 1964, 14.2, 15.0);
        Assert.Equal("PASS", rows[0].Result); // PPI
    }

    [Fact]
    public void BuildScorecard_LowDpiDisplay_FailsPpiCheck()
    {
        var info = new Models.DisplayInfo { ResolutionWidth = 1920, ResolutionHeight = 1080 };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, 1920, 1080, 27.0, 18.0);
        Assert.Equal("FAIL", rows[0].Result); // PPI
    }

    [Fact]
    public void BuildScorecard_GoodP3Gamut_PassesGamutCheck()
    {
        var info = new Models.DisplayInfo { GamutP3Pct = 98.5 };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, null, null, 0, 18.0);
        Assert.Equal("PASS", rows[1].Result); // DCI-P3
    }

    [Fact]
    public void BuildScorecard_LowP3Gamut_FailsGamutCheck()
    {
        var info = new Models.DisplayInfo { GamutP3Pct = 70.0 };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, null, null, 0, 18.0);
        Assert.Equal("FAIL", rows[1].Result);
    }

    [Fact]
    public void BuildScorecard_HighRefreshRate_Passes()
    {
        var info = new Models.DisplayInfo { RefreshRate = 144.0 };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, null, null, 0, 18.0);
        Assert.Equal("PASS", rows[2].Result);
    }

    [Fact]
    public void BuildScorecard_SixtyHzDisplay_ReviewResult()
    {
        var info = new Models.DisplayInfo { RefreshRate = 60.0 };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, null, null, 0, 18.0);
        Assert.Equal("REVIEW", rows[2].Result);
    }

    [Fact]
    public void BuildScorecard_HdrPresent_PassesHdrCheck()
    {
        var info = new Models.DisplayInfo { HdrTier = "HDR10" };
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, null, null, 0, 18.0);
        Assert.Equal("PASS", rows[3].Result);
    }

    [Fact]
    public void BuildScorecard_NoHdr_FailsHdrCheck()
    {
        var info = new Models.DisplayInfo();
        var (rows, _, _, _) = DisplayLogic.BuildScorecard(info, null, null, 0, 18.0);
        Assert.Equal("FAIL", rows[3].Result);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static byte[] BuildMinimalEdid()
    {
        var data = new byte[128];
        // EDID header
        data[0] = 0x00;
        data[1] = data[2] = data[3] = data[4] = data[5] = data[6] = 0xFF;
        data[7] = 0x00;
        // Default manufacture year
        data[17] = (byte)(2020 - 1990);
        return data;
    }
}
