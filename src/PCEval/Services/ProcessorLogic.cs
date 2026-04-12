using PCEval.Models;

namespace PCEval.Services;

/// <summary>
/// Performance tier database and processor scorecard logic.
/// Ported from processor_info.py.
/// </summary>
public static class ProcessorLogic
{
    // ── Apple M-series reference (M3 Pro = 100 baseline) ──────────────────
    public static readonly Dictionary<string, int> M3ProReference = new()
    {
        ["single_core"]  = 100,
        ["multi_core"]   = 100,
        ["efficiency"]   = 100,
        ["sustained"]    = 100,
        ["igpu"]         = 100,
        ["real_world"]   = 100,
        ["platform"]     = 100,
    };

    public static readonly PerformanceTier UnknownTier = new()
    {
        TierId  = "unknown",
        Label   = "Unknown / Unrecognised CPU",
        Vendor  = "Unknown",
        Arch    = "Unknown",
        MatchPatterns = [],
    };

    // ── Performance tier database ─────────────────────────────────────────
    public static readonly List<PerformanceTier> PerformanceTiers =
    [
        // Apple Silicon
        new() { TierId="apple_m4_ultra", Label="Apple M4 Ultra",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m4 ultra"],
            SingleCore=115, MultiCore=200, Efficiency=110, Sustained=110,
            Igpu=190, RealWorld=130, Platform=95, TypicalTdp=60, ActiveCooling=false,
            Strengths=["Extreme multi-core throughput","Best-in-class efficiency","Unified memory"],
            Weaknesses=["Mac-only ecosystem","Limited x86 app compatibility"] },

        new() { TierId="apple_m4_max", Label="Apple M4 Max",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m4 max"],
            SingleCore=112, MultiCore=160, Efficiency=108, Sustained=108,
            Igpu=160, RealWorld=120, Platform=95, TypicalTdp=45, ActiveCooling=false,
            Strengths=["Excellent multi-core","Very efficient","Unified memory"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m4_pro", Label="Apple M4 Pro",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m4 pro"],
            SingleCore=110, MultiCore=130, Efficiency=106, Sustained=106,
            Igpu=130, RealWorld=115, Platform=95, TypicalTdp=30, ActiveCooling=false,
            Strengths=["Fast single-core","Great efficiency","Unified memory"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m4", Label="Apple M4",
            Vendor="Apple", Arch="ARM", MatchPatterns=["apple m4"],
            SingleCore=108, MultiCore=110, Efficiency=104, Sustained=104,
            Igpu=115, RealWorld=110, Platform=95, TypicalTdp=15, ActiveCooling=false,
            Strengths=["Excellent efficiency","Fast single-core"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m3_ultra", Label="Apple M3 Ultra",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m3 ultra"],
            SingleCore=103, MultiCore=185, Efficiency=105, Sustained=105,
            Igpu=180, RealWorld=125, Platform=95, TypicalTdp=60, ActiveCooling=false,
            Strengths=["Extreme multi-core throughput","Best-in-class efficiency"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m3_max", Label="Apple M3 Max",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m3 max"],
            SingleCore=102, MultiCore=155, Efficiency=103, Sustained=103,
            Igpu=155, RealWorld=118, Platform=95, TypicalTdp=45, ActiveCooling=false,
            Strengths=["Excellent multi-core","Unified memory bandwidth"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m3_pro", Label="Apple M3 Pro",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m3 pro"],
            SingleCore=100, MultiCore=100, Efficiency=100, Sustained=100,
            Igpu=100, RealWorld=100, Platform=95, TypicalTdp=30, ActiveCooling=false,
            Strengths=["Balanced performance and efficiency","Unified memory"],
            Weaknesses=["Mac-only ecosystem","Limited app virtualization"] },

        new() { TierId="apple_m3", Label="Apple M3",
            Vendor="Apple", Arch="ARM", MatchPatterns=["apple m3"],
            SingleCore=98, MultiCore=85, Efficiency=98, Sustained=97,
            Igpu=88, RealWorld=93, Platform=95, TypicalTdp=15, ActiveCooling=false,
            Strengths=["Great efficiency","Fast single-core for the TDP"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m2_ultra", Label="Apple M2 Ultra",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m2 ultra"],
            SingleCore=96, MultiCore=175, Efficiency=96, Sustained=96,
            Igpu=165, RealWorld=118, Platform=95, TypicalTdp=60, ActiveCooling=false,
            Strengths=["Massive multi-core throughput"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m2_max", Label="Apple M2 Max",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m2 max"],
            SingleCore=95, MultiCore=145, Efficiency=95, Sustained=95,
            Igpu=145, RealWorld=112, Platform=95, TypicalTdp=40, ActiveCooling=false,
            Strengths=["Strong multi-core","Excellent efficiency"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m2_pro", Label="Apple M2 Pro",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m2 pro"],
            SingleCore=93, MultiCore=90, Efficiency=93, Sustained=92,
            Igpu=90, RealWorld=90, Platform=95, TypicalTdp=30, ActiveCooling=false,
            Strengths=["Good balance of performance and efficiency"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m2", Label="Apple M2",
            Vendor="Apple", Arch="ARM", MatchPatterns=["apple m2"],
            SingleCore=90, MultiCore=78, Efficiency=91, Sustained=90,
            Igpu=82, RealWorld=86, Platform=95, TypicalTdp=15, ActiveCooling=false,
            Strengths=["Great efficiency for everyday tasks"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m1_ultra", Label="Apple M1 Ultra",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m1 ultra"],
            SingleCore=87, MultiCore=160, Efficiency=88, Sustained=88,
            Igpu=150, RealWorld=110, Platform=95, TypicalTdp=60, ActiveCooling=false,
            Strengths=["Outstanding multi-core efficiency"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m1_pro", Label="Apple M1 Pro",
            Vendor="Apple", Arch="ARM", MatchPatterns=["m1 pro"],
            SingleCore=86, MultiCore=82, Efficiency=87, Sustained=86,
            Igpu=80, RealWorld=82, Platform=95, TypicalTdp=30, ActiveCooling=false,
            Strengths=["Pioneer of Apple's high-efficiency pro chips"],
            Weaknesses=["Mac-only ecosystem"] },

        new() { TierId="apple_m1", Label="Apple M1",
            Vendor="Apple", Arch="ARM", MatchPatterns=["apple m1"],
            SingleCore=84, MultiCore=72, Efficiency=85, Sustained=84,
            Igpu=72, RealWorld=78, Platform=95, TypicalTdp=15, ActiveCooling=false,
            Strengths=["Excellent efficiency, strong single-core"],
            Weaknesses=["Mac-only ecosystem"] },

        // Intel Core Ultra (Arrow Lake)
        new() { TierId="intel_core_ultra_9_285", Label="Intel Core Ultra 9 285",
            Vendor="Intel", Arch="x86_64", MatchPatterns=["ultra 9 285","core ultra 9 285"],
            SingleCore=105, MultiCore=125, Efficiency=62, Sustained=68,
            Igpu=52, RealWorld=95, Platform=88, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Very fast single-core","Strong multi-core","Full x86 app ecosystem"],
            Weaknesses=["High TDP","Thermal throttling under sustained load","Weaker iGPU vs M-series"] },

        new() { TierId="intel_core_ultra_7_265", Label="Intel Core Ultra 7 265",
            Vendor="Intel", Arch="x86_64", MatchPatterns=["ultra 7 265","core ultra 7 265"],
            SingleCore=102, MultiCore=115, Efficiency=60, Sustained=65,
            Igpu=50, RealWorld=92, Platform=88, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Strong single-core","Full x86 ecosystem"],
            Weaknesses=["Power hungry","Thermal management critical"] },

        new() { TierId="intel_core_ultra_5_245", Label="Intel Core Ultra 5 245",
            Vendor="Intel", Arch="x86_64", MatchPatterns=["ultra 5 245","core ultra 5 245"],
            SingleCore=98, MultiCore=98, Efficiency=58, Sustained=62,
            Igpu=48, RealWorld=88, Platform=88, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Competitive single-core","x86 compatibility"],
            Weaknesses=["Efficiency gap vs M-series"] },

        new() { TierId="intel_core_ultra_h", Label="Intel Core Ultra (H-series, 14th-gen)",
            Vendor="Intel", Arch="x86_64",
            MatchPatterns=["ultra 9 185h","ultra 7 165h","ultra 7 155h","ultra 5 125h","ultra 5 135h"],
            SingleCore=97, MultiCore=108, Efficiency=55, Sustained=60,
            Igpu=45, RealWorld=88, Platform=88, TypicalTdp=45, ActiveCooling=true,
            Strengths=["Good multi-core burst","x86 app ecosystem","NPU for AI tasks"],
            Weaknesses=["Significant efficiency gap vs M-series","Throttles under sustained load"] },

        new() { TierId="intel_core_i9_13", Label="Intel Core i9 13th-gen",
            Vendor="Intel", Arch="x86_64", MatchPatterns=["i9-13","i9 13"],
            SingleCore=99, MultiCore=118, Efficiency=45, Sustained=55,
            Igpu=35, RealWorld=90, Platform=88, TypicalTdp=125, ActiveCooling=true,
            Strengths=["Fast burst performance","Widest app compatibility"],
            Weaknesses=["Very high TDP","Poor efficiency","Weak integrated graphics"] },

        new() { TierId="intel_core_i7_13", Label="Intel Core i7 13th-gen",
            Vendor="Intel", Arch="x86_64", MatchPatterns=["i7-13","i7 13"],
            SingleCore=95, MultiCore=105, Efficiency=48, Sustained=55,
            Igpu=33, RealWorld=86, Platform=88, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Strong burst, wide ecosystem"],
            Weaknesses=["Efficiency lags behind M-series significantly"] },

        new() { TierId="intel_core_i5_13", Label="Intel Core i5 13th-gen",
            Vendor="Intel", Arch="x86_64", MatchPatterns=["i5-13","i5 13"],
            SingleCore=90, MultiCore=90, Efficiency=50, Sustained=52,
            Igpu=32, RealWorld=80, Platform=88, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Solid everyday performance","x86 compatibility"],
            Weaknesses=["Weaker multi-core vs M-series Pro","Efficiency gap"] },

        // AMD Ryzen
        new() { TierId="amd_ryzen_9_9000", Label="AMD Ryzen 9 9000-series (Zen 5)",
            Vendor="AMD", Arch="x86_64",
            MatchPatterns=["ryzen 9 9","ryzen 9 9900","ryzen 9 9950","ryzen 9 9700"],
            SingleCore=108, MultiCore=130, Efficiency=68, Sustained=72,
            Igpu=55, RealWorld=98, Platform=85, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Top-tier single-core","Best x86 multi-core","Improved efficiency"],
            Weaknesses=["Still trails M-series on efficiency per watt","Discrete GPU needed for graphics"] },

        new() { TierId="amd_ryzen_9_7000", Label="AMD Ryzen 9 7000-series (Zen 4)",
            Vendor="AMD", Arch="x86_64",
            MatchPatterns=["ryzen 9 7","ryzen 9 7900","ryzen 9 7950","ryzen 9 7700"],
            SingleCore=103, MultiCore=122, Efficiency=62, Sustained=68,
            Igpu=48, RealWorld=94, Platform=85, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Competitive single-core","Strong multi-core","x86 ecosystem"],
            Weaknesses=["Power-hungry under load","Weak integrated graphics"] },

        new() { TierId="amd_ryzen_7_9700x", Label="AMD Ryzen 7 9700X (Zen 5)",
            Vendor="AMD", Arch="x86_64", MatchPatterns=["ryzen 7 9700","ryzen 7 9800"],
            SingleCore=106, MultiCore=112, Efficiency=70, Sustained=72,
            Igpu=50, RealWorld=95, Platform=85, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Excellent single-core speed","Good efficiency for x86"],
            Weaknesses=["Discrete GPU required for serious graphics"] },

        new() { TierId="amd_ryzen_7_7700x", Label="AMD Ryzen 7 7000-series (Zen 4)",
            Vendor="AMD", Arch="x86_64", MatchPatterns=["ryzen 7 7700","ryzen 7 7800"],
            SingleCore=100, MultiCore=105, Efficiency=60, Sustained=65,
            Igpu=45, RealWorld=89, Platform=85, TypicalTdp=65, ActiveCooling=true,
            Strengths=["Strong everyday performance","x86 compatibility"],
            Weaknesses=["Efficiency gap vs M-series"] },

        new() { TierId="amd_ryzen_ai_hx", Label="AMD Ryzen AI HX (Strix Point / Hawk Point)",
            Vendor="AMD", Arch="x86_64",
            MatchPatterns=["ryzen ai 9 hx","ryzen ai 7 hx","ryzen ai 5 hx",
                           "ryzen ai 9 365","ryzen ai 9 370","ryzen ai 7 350"],
            SingleCore=100, MultiCore=110, Efficiency=72, Sustained=70,
            Igpu=78, RealWorld=92, Platform=85, TypicalTdp=28, ActiveCooling=true,
            Strengths=["Best x86 integrated graphics","Good NPU for AI workloads","Improved efficiency"],
            Weaknesses=["Sustained performance still lags M-series Pro"] },

        new() { TierId="amd_ryzen_hx", Label="AMD Ryzen HX (laptop, Zen 4/5)",
            Vendor="AMD", Arch="x86_64",
            MatchPatterns=["ryzen 9 hx","ryzen 7 hx","hx 370","hx 3d",
                           "ryzen 9 7945hx","ryzen 9 7940hx"],
            SingleCore=99, MultiCore=115, Efficiency=58, Sustained=62,
            Igpu=42, RealWorld=90, Platform=85, TypicalTdp=55, ActiveCooling=true,
            Strengths=["Strong laptop multi-core burst"],
            Weaknesses=["Thermal throttling under prolonged load","Efficiency gap"] },

        // Qualcomm Snapdragon X
        new() { TierId="qualcomm_x_elite", Label="Qualcomm Snapdragon X Elite",
            Vendor="Qualcomm", Arch="ARM",
            MatchPatterns=["snapdragon x elite","x1e","x elite"],
            SingleCore=95, MultiCore=100, Efficiency=88, Sustained=85,
            Igpu=75, RealWorld=82, Platform=70, TypicalTdp=23, ActiveCooling=true,
            Strengths=["Very efficient","Good integrated GPU","Strong NPU"],
            Weaknesses=["x86 app emulation overhead","Ecosystem maturity still growing"] },

        new() { TierId="qualcomm_x_plus", Label="Qualcomm Snapdragon X Plus",
            Vendor="Qualcomm", Arch="ARM",
            MatchPatterns=["snapdragon x plus","x1p","x plus"],
            SingleCore=92, MultiCore=85, Efficiency=86, Sustained=82,
            Igpu=68, RealWorld=78, Platform=70, TypicalTdp=23, ActiveCooling=true,
            Strengths=["Efficient ARM performance","Good battery life"],
            Weaknesses=["x86 app compatibility gaps","Smaller ecosystem than x86"] },
    ];

    // ── Tier matching ──────────────────────────────────────────────────────

    /// <summary>Return the best-matching tier for a CPU model string (case-insensitive).</summary>
    public static PerformanceTier MatchTier(string model)
    {
        string lower = model.ToLowerInvariant();
        foreach (var tier in PerformanceTiers)
            foreach (var pattern in tier.MatchPatterns)
                if (lower.Contains(pattern.ToLowerInvariant()))
                    return tier;
        return UnknownTier;
    }

    // ── Vendor / architecture inference ───────────────────────────────────

    public static string InferVendor(string model, string vendorId = "")
    {
        string combined = (model + " " + vendorId).ToLowerInvariant();
        if (combined.Contains("apple") || combined.Contains(" m1") ||
            combined.Contains(" m2") || combined.Contains(" m3") ||
            combined.Contains(" m4"))
            return "Apple";
        if (combined.Contains("qualcomm") || combined.Contains("snapdragon"))
            return "Qualcomm";
        if (combined.Contains("mediatek"))
            return "MediaTek";
        if (combined.Contains("amd") || combined.Contains("ryzen") ||
            combined.Contains("epyc"))
            return "AMD";
        if (combined.Contains("intel") || combined.Contains("core i") ||
            combined.Contains("core ultra") || combined.Contains("xeon"))
            return "Intel";
        return "Unknown";
    }

    public static string InferArch(string model, string vendorId = "")
    {
        string combined = (model + " " + vendorId).ToLowerInvariant();
        if (combined.Contains("apple") || combined.Contains("snapdragon") ||
            combined.Contains("arm") || combined.Contains("aarch64") ||
            combined.Contains("m1") || combined.Contains("m2") ||
            combined.Contains("m3") || combined.Contains("m4"))
            return "ARM";
        return "x86_64";
    }

    // ── Scorecard ──────────────────────────────────────────────────────────

    private const int PassThreshold   = 90;
    private const int ReviewThreshold = 70;

    private static readonly Color Green  = Color.FromArgb("#007a00");
    private static readonly Color Orange = Color.FromArgb("#b85c00");
    private static readonly Color Red    = Color.FromArgb("#cc0000");
    private static readonly Color Gray   = Color.FromArgb("#555555");

    private static Color ResultColor(string result) => result switch
    {
        "PASS"   => Green,
        "REVIEW" => Orange,
        "FAIL"   => Red,
        _        => Gray,
    };

    private static int? RelativeScore(int? score, int reference) =>
        score.HasValue && reference > 0
            ? (int?)Math.Min(Math.Round(score.Value / (double)reference * 100), 120)
            : null;

    private static string ScoreResult(int? pct) => pct switch
    {
        null                       => "REVIEW",
        >= PassThreshold           => "PASS",
        >= ReviewThreshold         => "REVIEW",
        _                          => "FAIL",
    };

    private static string ConsumerNote(string dimension, int? score, PerformanceTier tier)
    {
        if (score is null) return "No data available for this processor.";
        int? pct = RelativeScore(score, 100);
        if (pct is null) return "No data available.";
        return dimension switch
        {
            "single_core" => pct >= 105
                ? "Faster than M3 Pro for snappy app launches and UI responsiveness."
                : pct >= 90
                    ? "Comparable to M3 Pro for everyday tasks and app launches."
                    : "Noticeably slower than M3 Pro for single-threaded workloads.",
            "multi_core" => pct >= 120
                ? "Crushes M3 Pro on heavy parallel workloads (builds, exports)."
                : pct >= 100
                    ? "Matches or beats M3 Pro for multi-threaded tasks."
                    : pct >= 80
                        ? "Reasonably competitive with M3 Pro for most multi-threaded work."
                        : "Falls behind M3 Pro on heavy multi-threaded workloads.",
            "efficiency" => pct >= 95
                ? "Best-in-class battery life – rivals or exceeds M3 Pro efficiency."
                : pct >= 70
                    ? "Decent battery life but noticeably less efficient than M3 Pro."
                    : "Expect significantly shorter battery life compared to M3 Pro.",
            "sustained" => pct >= 95
                ? "Maintains peak performance under prolonged load – no throttling."
                : pct >= 70
                    ? "Mild throttling under sustained load; still usable for most tasks."
                    : "Significant throttling under sustained load – performance drops over time.",
            "igpu" => pct >= 95
                ? "Excellent integrated GPU – handles light gaming and media work well."
                : pct >= 70
                    ? "Adequate integrated GPU for everyday graphics tasks."
                    : "Weaker integrated GPU; a discrete GPU is recommended for media/graphics.",
            "real_world" => pct >= 95
                ? "Outstanding real-world performance across web, Office, and coding."
                : pct >= 80
                    ? "Good real-world performance for productivity and content creation."
                    : "Below M3 Pro in real-world productivity benchmarks.",
            "platform" => tier.Arch == "ARM" && !tier.Vendor.Equals("Apple", StringComparison.OrdinalIgnoreCase)
                ? "ARM Windows chip: growing ecosystem but some x86 apps may need emulation."
                : tier.Arch == "ARM"
                    ? "macOS ecosystem: excellent for Apple workflows; some x86 app gaps."
                    : "Full x86 Windows ecosystem: broadest app and game compatibility.",
            _ => "",
        };
    }

    private static string MakeBadge(string dimension, int? score, PerformanceTier tier)
    {
        if (score is null) return "";
        int? pct = RelativeScore(score, 100);
        if (pct is null) return "";
        return dimension switch
        {
            "efficiency" when pct >= 95  => "Best for battery life",
            "efficiency" when pct >= 70  => "Good battery life",
            "efficiency"                 => "Shorter battery life",
            "single_core" when pct >= 105 => "Snappiest UI",
            "multi_core" when pct >= 120  => "Powerhouse",
            "igpu" when pct >= 95         => "Great for media/graphics",
            "platform" when tier.Arch == "ARM" && !tier.Vendor.Equals("Apple", StringComparison.OrdinalIgnoreCase)
                                          => "ARM Windows",
            "platform" when tier.Arch == "ARM" => "macOS native",
            "platform"                    => "Windows x86",
            _ => "",
        };
    }

    /// <summary>Build the processor scorecard rows from a <see cref="Models.ProcessorInfo"/>.</summary>
    public static List<ProcessorScorecardRow> BuildScorecard(Models.ProcessorInfo info)
    {
        var rows = new List<ProcessorScorecardRow>();
        var tier = info.Tier ?? UnknownTier;

        var dimensions = new (string key, string metric)[]
        {
            ("single_core", "Single-Core Performance"),
            ("multi_core",  "Multi-Core Performance"),
            ("efficiency",  "Performance per Watt (Efficiency)"),
            ("sustained",   "Sustained Performance"),
            ("igpu",        "Integrated GPU / Accelerators"),
            ("real_world",  "Real-World Workloads"),
            ("platform",    "Platform & Ecosystem"),
        };

        foreach (var (key, metric) in dimensions)
        {
            int? score = key switch
            {
                "single_core" => tier.SingleCore,
                "multi_core"  => tier.MultiCore,
                "efficiency"  => tier.Efficiency,
                "sustained"   => tier.Sustained,
                "igpu"        => tier.Igpu,
                "real_world"  => tier.RealWorld,
                "platform"    => tier.Platform,
                _             => null,
            };

            int? pct    = RelativeScore(score, 100);
            string val  = pct.HasValue ? $"{pct}% of M3 Pro" : "Unknown";
            string res  = ScoreResult(pct);

            rows.Add(new ProcessorScorecardRow
            {
                Metric      = metric,
                Value       = val,
                Target      = "M3 Pro = 100%",
                Result      = res,
                ResultColor = ResultColor(res),
                Note        = ConsumerNote(key, score, tier),
                Badge       = MakeBadge(key, score, tier),
            });
        }

        // Strengths & weaknesses summary rows
        rows.Add(new ProcessorScorecardRow
        {
            Metric  = "Strengths",
            Value   = tier.Strengths.Length > 0 ? string.Join("; ", tier.Strengths) : "Unknown",
            Target  = "—",
            Result  = "—",
            ResultColor = Green,
            Note    = "Key advantages of this processor.",
        });
        rows.Add(new ProcessorScorecardRow
        {
            Metric  = "Weaknesses",
            Value   = tier.Weaknesses.Length > 0 ? string.Join("; ", tier.Weaknesses) : "Unknown",
            Target  = "—",
            Result  = "—",
            ResultColor = Red,
            Note    = "Key limitations compared to M-series.",
        });

        return rows;
    }

    public static (string Grade, Color Color, string Verdict)
        OverallGrade(List<ProcessorScorecardRow> rows)
    {
        var pcts = rows
            .Where(r => r.IsDimensionRow && r.Value != "Unknown")
            .Select(r =>
            {
                var m = System.Text.RegularExpressions.Regex.Match(r.Value, @"^(\d+)%");
                return m.Success ? (double?)double.Parse(m.Groups[1].Value) : null;
            })
            .OfType<double>()
            .ToList();

        if (pcts.Count == 0)
            return ("—", Color.FromArgb("#555555"), "Processor not recognised — install data unavailable");

        double overall = pcts.Average();
        string grade   = DisplayLogic.ScoreLabel(overall);
        Color  color   = DisplayLogic.ScoreColor(overall);
        string verdict = overall switch
        {
            >= 90 => "Comparable to or better than M3 Pro overall",
            >= 70 => "Competitive in most areas; some gaps vs M3 Pro",
            _     => "Noticeably behind M3 Pro in key dimensions",
        };
        return (grade, color, verdict);
    }
}
