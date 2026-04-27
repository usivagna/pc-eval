using PCEval.Models;
using DisplayInfo = PCEval.Models.DisplayInfo;

namespace PCEval.Services;

/// <summary>
/// Current Apple Mac lineup (April 2026 reference) and point-by-point
/// comparison logic against the user's PC.
/// </summary>
public static class MacLineupLogic
{
    // ── Result colours (mirror DisplayLogic / ProcessorLogic semantics) ──
    private static readonly Color Better = Color.FromArgb("#107C10"); // Win11 green
    private static readonly Color Match  = Color.FromArgb("#0078D4"); // Win11 accent
    private static readonly Color Worse  = Color.FromArgb("#C42B1C"); // Win11 red
    private static readonly Color Neutral = Color.FromArgb("#605E5C");

    // ── Lineup ────────────────────────────────────────────────────────────
    /// <summary>Current Mac lineup.</summary>
    public static readonly List<MacConfig> Lineup =
    [
        new() {
            Id = "mba13_m4", Label = "MacBook Air 13\" — M4", FormFactor = "Laptop",
            Chip = "Apple M4", ChipTierId = "apple_m4",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 13.6, DisplayWidth = 2560, DisplayHeight = 1664,
            DisplayPpi = 224, RefreshRateHz = 60, ProMotion = false,
            P3GamutPct = 99, HdrSupported = false,
            SustainedBrightnessNits = 500, PeakBrightnessNits = 500,
            StartingPriceUsd = 1099,
            Notes = "Fanless ultraportable; great battery, no HDR display.",
        },
        new() {
            Id = "mba15_m4", Label = "MacBook Air 15\" — M4", FormFactor = "Laptop",
            Chip = "Apple M4", ChipTierId = "apple_m4",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 15.3, DisplayWidth = 2880, DisplayHeight = 1864,
            DisplayPpi = 224, RefreshRateHz = 60, ProMotion = false,
            P3GamutPct = 99, HdrSupported = false,
            SustainedBrightnessNits = 500, PeakBrightnessNits = 500,
            StartingPriceUsd = 1299,
            Notes = "Larger Air; same M4 chip and 60 Hz panel.",
        },
        new() {
            Id = "mbp14_m4", Label = "MacBook Pro 14\" — M4", FormFactor = "Laptop",
            Chip = "Apple M4", ChipTierId = "apple_m4",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 14.2, DisplayWidth = 3024, DisplayHeight = 1964,
            DisplayPpi = 254, RefreshRateHz = 120, ProMotion = true,
            P3GamutPct = 99, HdrSupported = true,
            SustainedBrightnessNits = 1000, PeakBrightnessNits = 1600,
            StartingPriceUsd = 1599,
            Notes = "Liquid Retina XDR with ProMotion and HDR.",
        },
        new() {
            Id = "mbp14_m4_pro", Label = "MacBook Pro 14\" — M4 Pro", FormFactor = "Laptop",
            Chip = "Apple M4 Pro", ChipTierId = "apple_m4_pro",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 14.2, DisplayWidth = 3024, DisplayHeight = 1964,
            DisplayPpi = 254, RefreshRateHz = 120, ProMotion = true,
            P3GamutPct = 99, HdrSupported = true,
            SustainedBrightnessNits = 1000, PeakBrightnessNits = 1600,
            StartingPriceUsd = 1999,
            Notes = "Pro chip + XDR; the workhorse 14-inch.",
        },
        new() {
            Id = "mbp14_m4_max", Label = "MacBook Pro 14\" — M4 Max", FormFactor = "Laptop",
            Chip = "Apple M4 Max", ChipTierId = "apple_m4_max",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 14.2, DisplayWidth = 3024, DisplayHeight = 1964,
            DisplayPpi = 254, RefreshRateHz = 120, ProMotion = true,
            P3GamutPct = 99, HdrSupported = true,
            SustainedBrightnessNits = 1000, PeakBrightnessNits = 1600,
            StartingPriceUsd = 3199,
            Notes = "Top-tier 14-inch for creative pros.",
        },
        new() {
            Id = "mbp16_m4_pro", Label = "MacBook Pro 16\" — M4 Pro", FormFactor = "Laptop",
            Chip = "Apple M4 Pro", ChipTierId = "apple_m4_pro",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 16.2, DisplayWidth = 3456, DisplayHeight = 2234,
            DisplayPpi = 254, RefreshRateHz = 120, ProMotion = true,
            P3GamutPct = 99, HdrSupported = true,
            SustainedBrightnessNits = 1000, PeakBrightnessNits = 1600,
            StartingPriceUsd = 2499,
            Notes = "Larger XDR canvas for creative work.",
        },
        new() {
            Id = "mbp16_m4_max", Label = "MacBook Pro 16\" — M4 Max", FormFactor = "Laptop",
            Chip = "Apple M4 Max", ChipTierId = "apple_m4_max",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 16.2, DisplayWidth = 3456, DisplayHeight = 2234,
            DisplayPpi = 254, RefreshRateHz = 120, ProMotion = true,
            P3GamutPct = 99, HdrSupported = true,
            SustainedBrightnessNits = 1000, PeakBrightnessNits = 1600,
            StartingPriceUsd = 3499,
            Notes = "Flagship laptop; Max chip + 16\" XDR.",
        },
        new() {
            Id = "imac_m4", Label = "iMac 24\" — M4", FormFactor = "All-in-one",
            Chip = "Apple M4", ChipTierId = "apple_m4",
            HasBuiltInDisplay = true,
            DisplayDiagonalInches = 23.5, DisplayWidth = 4480, DisplayHeight = 2520,
            DisplayPpi = 218, RefreshRateHz = 60, ProMotion = false,
            P3GamutPct = 99, HdrSupported = false,
            SustainedBrightnessNits = 500, PeakBrightnessNits = 500,
            StartingPriceUsd = 1299,
            Notes = "4.5K Retina all-in-one for the home.",
        },
        new() {
            Id = "mini_m4", Label = "Mac mini — M4", FormFactor = "Mini",
            Chip = "Apple M4", ChipTierId = "apple_m4",
            HasBuiltInDisplay = false,
            StartingPriceUsd = 599,
            Notes = "Bring your own display; best price-per-perf in the lineup.",
        },
        new() {
            Id = "mini_m4_pro", Label = "Mac mini — M4 Pro", FormFactor = "Mini",
            Chip = "Apple M4 Pro", ChipTierId = "apple_m4_pro",
            HasBuiltInDisplay = false,
            StartingPriceUsd = 1399,
            Notes = "Pro chip in a tiny footprint.",
        },
        new() {
            Id = "studio_m4_max", Label = "Mac Studio — M4 Max", FormFactor = "Workstation",
            Chip = "Apple M4 Max", ChipTierId = "apple_m4_max",
            HasBuiltInDisplay = false,
            StartingPriceUsd = 1999,
            Notes = "Compact pro desktop; Max chip with sustained performance.",
        },
        new() {
            Id = "studio_m3_ultra", Label = "Mac Studio — M3 Ultra", FormFactor = "Workstation",
            Chip = "Apple M3 Ultra", ChipTierId = "apple_m3_ultra",
            HasBuiltInDisplay = false,
            StartingPriceUsd = 3999,
            Notes = "Ultra chip; extreme multi-core for heavy creative work.",
        },
        new() {
            Id = "macpro_m2_ultra", Label = "Mac Pro — M2 Ultra", FormFactor = "Workstation",
            Chip = "Apple M2 Ultra", ChipTierId = "apple_m2_ultra",
            HasBuiltInDisplay = false,
            StartingPriceUsd = 6999,
            Notes = "Tower with PCIe expansion; Apple's only modular Mac.",
        },
    ];

    // ── Helpers ───────────────────────────────────────────────────────────
    private static (string result, Color color) ResultFor(double pct) => pct switch
    {
        >= 105 => ("BETTER", Better),
        >= 95  => ("MATCH",  Match),
        _      => ("WORSE",  Worse),
    };

    private static (string result, Color color) BoolResult(bool? you, bool mac) =>
        (you, mac) switch
        {
            (true,  true)  => ("MATCH",  Match),
            (true,  false) => ("BETTER", Better),
            (false, true)  => ("WORSE",  Worse),
            (false, false) => ("MATCH",  Match),
            _              => ("—",      Neutral),
        };

    private static double? UserPpi(DisplayInfo? d)
    {
        if (d?.ResolutionWidth is not int w || d.ResolutionHeight is not int h
            || d.DiagonalInches is not double diag || diag <= 0)
            return null;
        return Math.Sqrt(w * (double)w + h * (double)h) / diag;
    }

    // ── Comparison ────────────────────────────────────────────────────────
    /// <summary>
    /// Build a point-by-point comparison of the user's PC against every Mac
    /// in <see cref="Lineup"/>.
    /// </summary>
    public static List<MacComparisonResult> BuildLineupComparison(
        ProcessorInfo? proc, DisplayInfo? display)
    {
        var results = new List<MacComparisonResult>();
        var userTier = proc?.Tier ?? ProcessorLogic.UnknownTier;
        var tiersById = ProcessorLogic.PerformanceTiers
            .ToDictionary(t => t.TierId, t => t);

        foreach (var mac in Lineup)
        {
            var rows = new List<MacComparisonRow>();
            tiersById.TryGetValue(mac.ChipTierId, out var macTier);
            macTier ??= ProcessorLogic.UnknownTier;

            // CPU dimensions ------------------------------------------------
            void AddCpuRow(string metric, int? userScore, int? macScore, string note)
            {
                if (userScore is null || macScore is null || macScore == 0)
                {
                    rows.Add(new MacComparisonRow {
                        Metric = metric,
                        YourValue = userScore?.ToString() ?? "Unknown",
                        MacValue = macScore?.ToString() ?? "Unknown",
                        Result = "—", ResultColor = Neutral, Note = note,
                    });
                    return;
                }
                double pct = userScore.Value / (double)macScore.Value * 100.0;
                var (res, col) = ResultFor(pct);
                rows.Add(new MacComparisonRow {
                    Metric = metric,
                    YourValue = userScore.ToString()!,
                    MacValue  = macScore.ToString()!,
                    Result    = res,
                    ResultColor = col,
                    Note      = $"{pct:0}% of {mac.Chip} — {note}",
                });
            }

            AddCpuRow("CPU Single-Core",      userTier.SingleCore, macTier.SingleCore, "burst & UI snappiness");
            AddCpuRow("CPU Multi-Core",       userTier.MultiCore,  macTier.MultiCore,  "parallel workloads");
            AddCpuRow("Performance / Watt",   userTier.Efficiency, macTier.Efficiency, "battery life proxy");
            AddCpuRow("Sustained Performance", userTier.Sustained, macTier.Sustained, "no throttle under load");
            AddCpuRow("Integrated GPU",       userTier.Igpu,       macTier.Igpu,       "iGPU / media engine");
            AddCpuRow("Real-World",           userTier.RealWorld,  macTier.RealWorld,  "everyday productivity");

            // Display dimensions (only for Macs with a built-in display) ---
            if (mac.HasBuiltInDisplay)
            {
                // PPI
                double? userPpi = UserPpi(display);
                if (userPpi.HasValue && mac.DisplayPpi.HasValue)
                {
                    double pct = userPpi.Value / mac.DisplayPpi.Value * 100.0;
                    var (res, col) = ResultFor(pct);
                    rows.Add(new MacComparisonRow {
                        Metric = "Pixel Density",
                        YourValue = $"{userPpi.Value:0} PPI",
                        MacValue  = $"{mac.DisplayPpi} PPI",
                        Result = res, ResultColor = col,
                        Note = $"{pct:0}% of Mac — sharper text & images at higher PPI",
                    });
                }
                else
                {
                    rows.Add(new MacComparisonRow {
                        Metric = "Pixel Density",
                        YourValue = userPpi.HasValue ? $"{userPpi.Value:0} PPI" : "Unknown",
                        MacValue = $"{mac.DisplayPpi} PPI",
                        Result = "—", ResultColor = Neutral,
                        Note = "Enter your screen diagonal on the Display tab.",
                    });
                }

                // Refresh rate
                if (display?.RefreshRate is double hz && mac.RefreshRateHz is double macHz)
                {
                    double pct = hz / macHz * 100.0;
                    var (res, col) = ResultFor(pct);
                    string macLabel = mac.ProMotion ? $"{macHz:0} Hz ProMotion" : $"{macHz:0} Hz";
                    rows.Add(new MacComparisonRow {
                        Metric = "Refresh Rate",
                        YourValue = $"{hz:0} Hz",
                        MacValue  = macLabel,
                        Result = res, ResultColor = col,
                        Note = pct >= 100
                            ? "Equal-or-smoother motion than this Mac."
                            : "Lower refresh than this Mac — less smooth scrolling/animation.",
                    });
                }

                // P3 gamut
                if (display?.GamutP3Pct is double p3 && mac.P3GamutPct is double macP3)
                {
                    double pct = p3 / macP3 * 100.0;
                    var (res, col) = ResultFor(pct);
                    rows.Add(new MacComparisonRow {
                        Metric = "DCI-P3 Gamut",
                        YourValue = $"{p3:0}%",
                        MacValue  = $"{macP3:0}%",
                        Result = res, ResultColor = col,
                        Note = "Wide-color coverage for photo/video editing.",
                    });
                }
                else if (mac.P3GamutPct.HasValue)
                {
                    rows.Add(new MacComparisonRow {
                        Metric = "DCI-P3 Gamut",
                        YourValue = "Unknown",
                        MacValue  = $"{mac.P3GamutPct:0}%",
                        Result = "—", ResultColor = Neutral,
                        Note = "Mac panels cover ~99% of DCI-P3.",
                    });
                }

                // HDR
                if (display?.HdrSupported is bool youHdr)
                {
                    var (res, col) = BoolResult(youHdr, mac.HdrSupported);
                    rows.Add(new MacComparisonRow {
                        Metric = "HDR Support",
                        YourValue = youHdr ? "Yes" : "No",
                        MacValue  = mac.HdrSupported ? "Yes (XDR)" : "No",
                        Result = res, ResultColor = col,
                        Note = mac.HdrSupported
                            ? $"Mac peaks ~{mac.PeakBrightnessNits} nits HDR."
                            : "Neither targets HDR.",
                    });
                }
                else
                {
                    rows.Add(new MacComparisonRow {
                        Metric = "HDR Support",
                        YourValue = "Unknown",
                        MacValue  = mac.HdrSupported ? "Yes (XDR)" : "No",
                        Result = "—", ResultColor = Neutral,
                    });
                }

                // Brightness (sustained)
                if (display?.MaxLuminanceNits is int nits && mac.SustainedBrightnessNits is int macNits && macNits > 0)
                {
                    double pct = nits / (double)macNits * 100.0;
                    var (res, col) = ResultFor(pct);
                    rows.Add(new MacComparisonRow {
                        Metric = "Brightness (sustained)",
                        YourValue = $"{nits} nits",
                        MacValue  = $"{macNits} nits",
                        Result = res, ResultColor = col,
                        Note = "Important for outdoor / bright-room use.",
                    });
                }
            }

            // Overall verdict -----------------------------------------------
            var scored = rows.Where(r => r.Result is "BETTER" or "MATCH" or "WORSE").ToList();
            if (scored.Count == 0)
            {
                results.Add(new MacComparisonResult {
                    Mac = mac, Rows = rows,
                    OverallPct = 0,
                    Verdict = "No data",
                    VerdictColor = Neutral,
                    Summary = "Not enough info to score this Mac.",
                });
                continue;
            }

            int better = scored.Count(r => r.Result == "BETTER");
            int match  = scored.Count(r => r.Result == "MATCH");
            int worse  = scored.Count(r => r.Result == "WORSE");

            string verdict;
            Color verdictColor;
            string summary;
            if (better >= worse + 2)
            {
                verdict = "Beats";
                verdictColor = Better;
                summary = $"Your PC beats {mac.Label.Split('—')[0].Trim()} on {better} of {scored.Count} points.";
            }
            else if (worse >= better + 2)
            {
                verdict = "Trails";
                verdictColor = Worse;
                summary = $"Your PC trails {mac.Label.Split('—')[0].Trim()} on {worse} of {scored.Count} points.";
            }
            else if (better == 0 && worse == 0)
            {
                verdict = "Matches";
                verdictColor = Match;
                summary = $"Your PC matches {mac.Label.Split('—')[0].Trim()} across the board.";
            }
            else
            {
                verdict = "Mixed";
                verdictColor = Color.FromArgb("#B85C00");
                summary = $"Mixed — better on {better}, equal on {match}, worse on {worse}.";
            }

            results.Add(new MacComparisonResult {
                Mac = mac, Rows = rows,
                OverallPct = scored.Count == 0 ? 0 : (better * 110 + match * 100 + worse * 70) / (double)scored.Count,
                Verdict = verdict,
                VerdictColor = verdictColor,
                Summary = summary,
            });
        }

        return results;
    }
}
