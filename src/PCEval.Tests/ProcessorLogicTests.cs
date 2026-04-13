using PCEval.Models;
using PCEval.Services;
using Xunit;

namespace PCEval.Tests;

/// <summary>
/// Tests for ProcessorLogic — tier matching, scorecard, and utility functions.
/// </summary>
public class ProcessorLogicTests
{
    // ── Tier matching ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Apple M3 Pro",                  "apple_m3_pro")]
    [InlineData("Apple M4 Pro",                  "apple_m4_pro")]
    [InlineData("Apple M2",                      "apple_m2")]
    [InlineData("Intel Core Ultra 9 285K",       "intel_core_ultra_9_285")]
    [InlineData("AMD Ryzen 9 7950X",             "amd_ryzen_9_7000")]
    [InlineData("Qualcomm Snapdragon X Elite",   "qualcomm_x_elite")]
    public void MatchTier_KnownChips_MatchCorrectTier(string model, string expectedTierId)
    {
        var tier = ProcessorLogic.MatchTier(model);
        Assert.Equal(expectedTierId, tier.TierId);
    }

    [Fact]
    public void MatchTier_UnknownCpu_ReturnsUnknownTier()
    {
        var tier = ProcessorLogic.MatchTier("Some Unknown Chip XYZ123");
        Assert.Equal("unknown", tier.TierId);
    }

    [Fact]
    public void MatchTier_CaseInsensitive_Matches()
    {
        var tier = ProcessorLogic.MatchTier("APPLE M3 PRO");
        Assert.Equal("apple_m3_pro", tier.TierId);
    }

    // ── Vendor inference ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Apple M3 Pro",                   "Apple")]
    [InlineData("Intel Core i7-13700K",           "Intel")]
    [InlineData("AMD Ryzen 9 5900X",              "AMD")]
    [InlineData("Qualcomm Snapdragon X Elite",    "Qualcomm")]
    public void InferVendor_CommonChips_CorrectVendor(string model, string expectedVendor) =>
        Assert.Equal(expectedVendor, ProcessorLogic.InferVendor(model));

    // ── Architecture inference ────────────────────────────────────────────

    [Theory]
    [InlineData("Apple M2",                       "ARM")]
    [InlineData("Qualcomm Snapdragon X Elite",    "ARM")]
    [InlineData("Intel Core i9-13900K",           "x86_64")]
    [InlineData("AMD Ryzen 9 7950X",              "x86_64")]
    public void InferArch_CommonChips_CorrectArch(string model, string expectedArch) =>
        Assert.Equal(expectedArch, ProcessorLogic.InferArch(model));

    // ── Scorecard ──────────────────────────────────────────────────────────

    [Fact]
    public void BuildScorecard_M3Pro_AllPassOrReview()
    {
        var info = new ProcessorInfo
        {
            CpuModel = "Apple M3 Pro",
            Tier     = ProcessorLogic.MatchTier("Apple M3 Pro"),
        };
        var rows = ProcessorLogic.BuildScorecard(info);

        var dimensionRows = rows.Where(r => r.IsDimensionRow).ToList();
        Assert.Equal(7, dimensionRows.Count);
        Assert.All(dimensionRows, r =>
            Assert.True(r.Result is "PASS" or "REVIEW",
                $"Unexpected result '{r.Result}' for metric '{r.Metric}'"));
    }

    [Fact]
    public void BuildScorecard_UnknownCpu_AllReview()
    {
        var info = new ProcessorInfo
        {
            CpuModel = "Unknown Chip",
            Tier     = ProcessorLogic.UnknownTier,
        };
        var rows = ProcessorLogic.BuildScorecard(info);
        var dimensionRows = rows.Where(r => r.IsDimensionRow).ToList();
        Assert.All(dimensionRows, r => Assert.Equal("REVIEW", r.Result));
    }

    [Fact]
    public void BuildScorecard_IncludesStrengthsAndWeaknesses()
    {
        var info = new ProcessorInfo
        {
            CpuModel = "Apple M3 Pro",
            Tier     = ProcessorLogic.MatchTier("Apple M3 Pro"),
        };
        var rows = ProcessorLogic.BuildScorecard(info);
        Assert.Contains(rows, r => r.Metric == "Strengths");
        Assert.Contains(rows, r => r.Metric == "Weaknesses");
    }

    [Fact]
    public void BuildScorecard_RowsHaveNonEmptyInsight()
    {
        var info = new ProcessorInfo
        {
            CpuModel = "Intel Core i9-13900K",
            Tier     = ProcessorLogic.MatchTier("i9-13900K"),
        };
        var rows     = ProcessorLogic.BuildScorecard(info);
        var dimRows  = rows.Where(r => r.IsDimensionRow).ToList();
        Assert.All(dimRows, r =>
            Assert.False(string.IsNullOrWhiteSpace(r.Note),
                $"No note for metric '{r.Metric}'"));
    }

    // ── OverallGrade ──────────────────────────────────────────────────────

    [Fact]
    public void OverallGrade_M3Pro_HighGrade()
    {
        var info = new ProcessorInfo
        {
            CpuModel = "Apple M3 Pro",
            Tier     = ProcessorLogic.MatchTier("Apple M3 Pro"),
        };
        var rows = ProcessorLogic.BuildScorecard(info);
        var (grade, _, _) = ProcessorLogic.OverallGrade(rows);
        Assert.True(grade is "A" or "B",
            $"Expected high grade for M3 Pro, got '{grade}'");
    }

    [Fact]
    public void OverallGrade_UnknownCpu_ReturnsDash()
    {
        var info = new ProcessorInfo
        {
            CpuModel = "Unknown Chip",
            Tier     = ProcessorLogic.UnknownTier,
        };
        var rows   = ProcessorLogic.BuildScorecard(info);
        var (grade, _, verdict) = ProcessorLogic.OverallGrade(rows);
        Assert.Equal("—", grade);
        Assert.Contains("not recognised", verdict, StringComparison.OrdinalIgnoreCase);
    }

    // ── Tier database completeness ────────────────────────────────────────

    [Fact]
    public void PerformanceTiers_AllHaveLabels()
    {
        Assert.All(ProcessorLogic.PerformanceTiers, t =>
            Assert.False(string.IsNullOrWhiteSpace(t.Label),
                $"Tier '{t.TierId}' has no label"));
    }

    [Fact]
    public void PerformanceTiers_AllHaveMatchPatterns()
    {
        Assert.All(ProcessorLogic.PerformanceTiers, t =>
            Assert.True(t.MatchPatterns.Length > 0,
                $"Tier '{t.TierId}' has no match patterns"));
    }

    [Fact]
    public void PerformanceTiers_ContainsAppleM3Pro()
    {
        Assert.Contains(ProcessorLogic.PerformanceTiers, t => t.TierId == "apple_m3_pro");
    }
}
