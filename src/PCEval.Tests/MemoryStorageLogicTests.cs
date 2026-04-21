using PCEval.Models;
using PCEval.Services;
using Xunit;

namespace PCEval.Tests;

/// <summary>
/// Tests for MemoryStorageLogic — scorecard building and overall grade.
/// </summary>
public class MemoryStorageLogicTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MemoryStorageInfo MakeInfo(
        double ramGb          = 16,
        string? ramType       = "DDR5",
        int?    ramSpeedMhz   = 4800,
        double  primaryGb     = 512,
        string? storageType   = "NVMe SSD")
    {
        return new MemoryStorageInfo
        {
            TotalRamBytes        = (long)(ramGb * 1_073_741_824),
            RamType              = ramType,
            RamSpeedMhz          = ramSpeedMhz,
            PrimaryStorageBytes  = (long)(primaryGb * 1_073_741_824),
            StorageType          = storageType,
        };
    }

    // ── BuildScorecard: row count and structure ────────────────────────────

    [Fact]
    public void BuildScorecard_ReturnsFiveDimensionRows()
    {
        var info = MakeInfo();
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var dimRows = rows.Where(r => r.IsDimensionRow).ToList();
        Assert.Equal(5, dimRows.Count);
    }

    [Fact]
    public void BuildScorecard_ReturnsUpgradeInfoRow()
    {
        var info = MakeInfo();
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var infoRows = rows.Where(r => !r.IsDimensionRow).ToList();
        Assert.Single(infoRows);
        Assert.Equal("PC Upgrade Advantage", infoRows[0].Metric);
    }

    [Fact]
    public void BuildScorecard_AllDimensionRowsHaveNotes()
    {
        var info = MakeInfo();
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var dimRows = rows.Where(r => r.IsDimensionRow).ToList();
        Assert.All(dimRows, r =>
            Assert.False(string.IsNullOrWhiteSpace(r.Note),
                $"No note for metric '{r.Metric}'"));
    }

    [Fact]
    public void BuildScorecard_AllDimensionRowsHaveTargets()
    {
        var info = MakeInfo();
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var dimRows = rows.Where(r => r.IsDimensionRow).ToList();
        Assert.All(dimRows, r =>
            Assert.False(string.IsNullOrWhiteSpace(r.Target),
                $"No target for metric '{r.Metric}'"));
    }

    // ── RAM Capacity scoring ─────────────────────────────────────────────────

    [Theory]
    [InlineData(32, "PASS")]
    [InlineData(64, "PASS")]
    [InlineData(16, "REVIEW")]
    [InlineData(24, "REVIEW")]
    [InlineData(8,  "FAIL")]
    [InlineData(4,  "FAIL")]
    public void BuildScorecard_RamCapacity_CorrectResult(double ramGb, string expected)
    {
        var info = MakeInfo(ramGb: ramGb);
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var row = rows.Single(r => r.Metric == "RAM Capacity");
        Assert.Equal(expected, row.Result);
    }

    // ── RAM Speed scoring ────────────────────────────────────────────────────

    [Theory]
    [InlineData("DDR5",   null,  "PASS")]
    [InlineData("LPDDR5", null,  "PASS")]
    [InlineData("DDR4",   null,  "REVIEW")]
    [InlineData("LPDDR4", null,  "REVIEW")]
    [InlineData("DDR3",   null,  "FAIL")]
    [InlineData(null,     4800,  "PASS")]
    [InlineData(null,     3200,  "REVIEW")]
    [InlineData(null,     1600,  "FAIL")]
    public void BuildScorecard_RamSpeed_CorrectResult(string? ramType, int? speedMhz, string expected)
    {
        var info = MakeInfo(ramType: ramType, ramSpeedMhz: speedMhz);
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var row = rows.Single(r => r.Metric == "RAM Speed / Type");
        Assert.Equal(expected, row.Result);
    }

    // ── Storage capacity scoring ─────────────────────────────────────────────

    [Theory]
    [InlineData(512,  "PASS")]
    [InlineData(1024, "PASS")]
    [InlineData(256,  "REVIEW")]
    [InlineData(480,  "REVIEW")]
    [InlineData(128,  "FAIL")]
    public void BuildScorecard_StorageCapacity_CorrectResult(double storGb, string expected)
    {
        var info = MakeInfo(primaryGb: storGb);
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var row = rows.Single(r => r.Metric == "Storage Capacity");
        Assert.Equal(expected, row.Result);
    }

    // ── Storage type scoring ─────────────────────────────────────────────────

    [Theory]
    [InlineData("NVMe SSD",  "PASS")]
    [InlineData("SATA SSD",  "REVIEW")]
    [InlineData("HDD",       "FAIL")]
    [InlineData("Unknown",   "FAIL")]
    [InlineData(null,        "FAIL")]
    public void BuildScorecard_StorageType_CorrectResult(string? storageType, string expected)
    {
        var info = MakeInfo(storageType: storageType);
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var row = rows.Single(r => r.Metric == "Storage Type");
        Assert.Equal(expected, row.Result);
    }

    // ── Storage speed scoring ────────────────────────────────────────────────

    [Theory]
    [InlineData("NVMe SSD",  "REVIEW")]   // Gen4 assumed for generic NVMe
    [InlineData("SATA SSD",  "FAIL")]
    [InlineData("HDD",       "FAIL")]
    public void BuildScorecard_StorageSpeed_CorrectResult(string? storageType, string expected)
    {
        var info = MakeInfo(storageType: storageType);
        var rows = MemoryStorageLogic.BuildScorecard(info);

        var row = rows.Single(r => r.Metric == "Est. Storage Read Speed");
        Assert.Equal(expected, row.Result);
    }

    // ── OverallGrade ──────────────────────────────────────────────────────────

    [Fact]
    public void OverallGrade_HighEndSystem_HighGrade()
    {
        var info = MakeInfo(ramGb: 64, ramType: "DDR5", primaryGb: 2048, storageType: "NVMe SSD");
        var rows = MemoryStorageLogic.BuildScorecard(info);
        var (grade, _, _) = MemoryStorageLogic.OverallGrade(rows);

        Assert.True(grade is "A" or "B",
            $"Expected high grade for high-end system, got '{grade}'");
    }

    [Fact]
    public void OverallGrade_LowEndSystem_LowGrade()
    {
        var info = MakeInfo(ramGb: 4, ramType: "DDR3", primaryGb: 128, storageType: "HDD");
        var rows = MemoryStorageLogic.BuildScorecard(info);
        var (grade, _, _) = MemoryStorageLogic.OverallGrade(rows);

        Assert.True(grade is "D" or "F",
            $"Expected low grade for low-end system, got '{grade}'");
    }

    [Fact]
    public void OverallGrade_EmptyRows_ReturnsDash()
    {
        var (grade, _, verdict) = MemoryStorageLogic.OverallGrade([]);
        Assert.Equal("—", grade);
        Assert.False(string.IsNullOrWhiteSpace(verdict));
    }

    [Fact]
    public void OverallGrade_ReturnsNonEmptyVerdict()
    {
        var info = MakeInfo();
        var rows = MemoryStorageLogic.BuildScorecard(info);
        var (_, _, verdict) = MemoryStorageLogic.OverallGrade(rows);

        Assert.False(string.IsNullOrWhiteSpace(verdict));
    }

    // ── Upgrade info row ──────────────────────────────────────────────────────

    [Fact]
    public void BuildScorecard_WithSlotInfo_ShowsFreeSlots()
    {
        var info = MakeInfo();
        info.RamSlots    = 4;
        info.RamSlotsUsed = 2;

        var rows = MemoryStorageLogic.BuildScorecard(info);
        var upgradeRow = rows.Single(r => r.Metric == "PC Upgrade Advantage");

        Assert.Contains("2 slot(s) free", upgradeRow.Note);
    }

    [Fact]
    public void BuildScorecard_AllSlotsFull_MentionsReplaceable()
    {
        var info = MakeInfo();
        info.RamSlots    = 2;
        info.RamSlotsUsed = 2;

        var rows = MemoryStorageLogic.BuildScorecard(info);
        var upgradeRow = rows.Single(r => r.Metric == "PC Upgrade Advantage");

        Assert.Contains("replaceable", upgradeRow.Note, StringComparison.OrdinalIgnoreCase);
    }
}
