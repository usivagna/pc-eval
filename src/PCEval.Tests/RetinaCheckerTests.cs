using PCEval.Services;
using Xunit;

namespace PCEval.Tests;

/// <summary>
/// Tests for the Retina Display Checker calculations.
/// Mirrors the logic validated in test_retina_checker.py.
/// </summary>
public class RetinaCheckerTests
{
    // ── RetinaMinPpi ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(18, 190.0, 200.0)]  // 18-inch: ~191 PPI threshold
    [InlineData(10, 340.0, 360.0)]  // 10-inch: ~344 PPI threshold
    [InlineData(30, 110.0, 120.0)]  // 30-inch: ~115 PPI threshold
    [InlineData(15, 225.0, 235.0)]  // 15-inch: ~229 PPI threshold
    public void RetinaMinPpi_ReturnsExpectedRange(
        double distanceIn, double expectedMin, double expectedMax)
    {
        double ppi = DisplayLogic.RetinaMinPpi(distanceIn);
        Assert.InRange(ppi, expectedMin, expectedMax);
    }

    [Fact]
    public void RetinaMinPpi_LongerDistance_LowerThreshold()
    {
        double near = DisplayLogic.RetinaMinPpi(15);
        double far  = DisplayLogic.RetinaMinPpi(30);
        Assert.True(near > far, "Closer distance should require higher PPI");
    }

    // ── CalculatePpi ──────────────────────────────────────────────────────

    [Fact]
    public void CalculatePpi_FullHd27Inch_CorrectPpi()
    {
        // 1920×1080 @ 27" ≈ 81.6 PPI
        double ppi = DisplayLogic.CalculatePpi(1920, 1080, 27.0);
        Assert.InRange(ppi, 81.0, 82.5);
    }

    [Fact]
    public void CalculatePpi_Retina5K27Inch_HighPpi()
    {
        // 5120×2880 @ 27" ≈ 218 PPI (iMac 5K)
        double ppi = DisplayLogic.CalculatePpi(5120, 2880, 27.0);
        Assert.InRange(ppi, 215.0, 222.0);
    }

    [Fact]
    public void CalculatePpi_MacBookPro16_HighPpi()
    {
        // 3456×2234 @ 16.2" ≈ 254 PPI
        double ppi = DisplayLogic.CalculatePpi(3456, 2234, 16.2);
        Assert.InRange(ppi, 248.0, 262.0);
    }

    // ── Retina pass/fail ──────────────────────────────────────────────────

    [Fact]
    public void RetinaCheck_MacBookProAt15Inches_IsRetina()
    {
        // 3456×2234 @ 16.2" @ 15" viewing distance → should be Retina
        double ppi    = DisplayLogic.CalculatePpi(3456, 2234, 16.2);
        double minPpi = DisplayLogic.RetinaMinPpi(15);
        Assert.True(ppi >= minPpi, $"Expected Retina: ppi={ppi:F1}, minPpi={minPpi:F1}");
    }

    [Fact]
    public void RetinaCheck_FullHdTvAt10Feet_IsRetina()
    {
        // A 55" 1920×1080 TV qualifies as Retina at 10 feet (120") viewing distance
        // 55" 1080p ≈ 40 PPI; min Retina at 120" ≈ 28.7 PPI → qualifies
        double ppi    = DisplayLogic.CalculatePpi(1920, 1080, 55.0);
        double minPpi = DisplayLogic.RetinaMinPpi(120);
        Assert.True(ppi >= minPpi, $"ppi={ppi:F1}, minPpi={minPpi:F1}");
    }

    [Fact]
    public void RetinaCheck_FullHd27InchAt18Inches_NotRetina()
    {
        // 1920×1080 @ 27" at 18" viewing distance → not Retina (81 PPI < ~191 PPI)
        double ppi    = DisplayLogic.CalculatePpi(1920, 1080, 27.0);
        double minPpi = DisplayLogic.RetinaMinPpi(18);
        Assert.True(ppi < minPpi, $"Expected NOT Retina: ppi={ppi:F1}, minPpi={minPpi:F1}");
    }

    // ── Edge cases ────────────────────────────────────────────────────────

    [Fact]
    public void CalculatePpi_SquareDisplay_CorrectResult()
    {
        // 1000×1000 @ 10" → diagonal = √2 * 1000 / 10 ≈ 141.4 PPI
        double ppi = DisplayLogic.CalculatePpi(1000, 1000, 10.0);
        Assert.InRange(ppi, 140.0, 142.5);
    }

    [Fact]
    public void RetinaMinPpi_VeryLargeDistance_VeryLowThreshold()
    {
        double ppi = DisplayLogic.RetinaMinPpi(1000);
        Assert.True(ppi < 5.0, $"At 1000 inches, threshold should be very low, got {ppi:F3}");
    }
}
