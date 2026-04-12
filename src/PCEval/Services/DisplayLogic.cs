using PCEval.Models;

namespace PCEval.Services;

/// <summary>
/// Core EDID parsing, gamut-coverage maths, and display scorecard logic.
/// Ported from display_info.py.
/// </summary>
public static class DisplayLogic
{
    // ── Standard colour-space primaries (CIE xy) ──────────────────────────
    public static readonly (double x, double y)[] SrgbPrimaries =
        [(0.640, 0.330), (0.300, 0.600), (0.150, 0.060)];

    public static readonly (double x, double y)[] DciP3Primaries =
        [(0.680, 0.320), (0.265, 0.690), (0.150, 0.060)];

    public static readonly (double x, double y)[] AdobeRgbPrimaries =
        [(0.640, 0.330), (0.210, 0.710), (0.150, 0.060)];

    private static readonly byte[] EdidHeader =
        [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    private const int DescSerialNumber = 0xFF;
    private const int DescMonitorName  = 0xFC;
    private const int DescRangeLimits  = 0xFD;

    private static readonly Dictionary<string, string> ManufacturerIds = new()
    {
        ["APP"] = "Apple",
        ["SAM"] = "Samsung Electronics",
        ["SDC"] = "Samsung Display",
        ["LEN"] = "Lenovo",
        ["DEL"] = "Dell",
        ["LGD"] = "LG Display",
        ["LGE"] = "LG Electronics",
        ["AUO"] = "AU Optronics",
        ["CMN"] = "Chimei Innolux",
        ["BOE"] = "BOE Technology",
        ["SHP"] = "Sharp",
        ["IVO"] = "InfoVision Optoelectronics",
        ["HWP"] = "Hewlett-Packard",
        ["ACR"] = "Acer",
        ["NEC"] = "NEC Display Solutions",
        ["VSC"] = "ViewSonic",
        ["BNQ"] = "BenQ",
        ["MSI"] = "MSI",
        ["AOC"] = "AOC International",
        ["ASU"] = "ASUS",
        ["AUS"] = "ASUS",
        ["PHI"] = "Philips",
        ["EIZ"] = "EIZO",
        ["PHL"] = "Philips Consumer Electronics",
        ["SNY"] = "Sony",
    };

    private static readonly Dictionary<int, string> DigitalInterface = new()
    {
        [0x00] = "Undefined",
        [0x01] = "DVI",
        [0x02] = "HDMI-a",
        [0x03] = "HDMI-b",
        [0x04] = "MDDI",
        [0x05] = "DisplayPort",
    };

    // ── EDID manufacturer ID decoding ─────────────────────────────────────

    /// <summary>Decode a 2-byte big-endian EDID manufacturer field to a 3-letter code.</summary>
    public static string DecodeManufacturerId(ushort raw)
    {
        char c1 = (char)(((raw >> 10) & 0x1F) + 64);
        char c2 = (char)(((raw >> 5)  & 0x1F) + 64);
        char c3 = (char)(((raw)       & 0x1F) + 64);
        return $"{c1}{c2}{c3}";
    }

    private static double ChromaCoord(byte msbByte, byte lsbByte, int lsbShift)
    {
        int lsb = (lsbByte >> lsbShift) & 0x03;
        return ((msbByte << 2) | lsb) / 1024.0;
    }

    // ── EDID parsing ──────────────────────────────────────────────────────

    /// <summary>
    /// Parse a raw EDID binary blob (128 bytes or more) and populate a
    /// <see cref="DisplayInfo"/>.
    /// </summary>
    public static DisplayInfo ParseEdid(byte[] data)
    {
        var result = new DisplayInfo();
        if (data.Length < 128) return result;
        for (int i = 0; i < 8; i++)
            if (data[i] != EdidHeader[i]) return result;

        // ── Manufacturer & product ─────────────────────────────────────────
        ushort mfrRaw = (ushort)((data[8] << 8) | data[9]);
        string mfrId  = DecodeManufacturerId(mfrRaw);
        result.ManufacturerId   = mfrId;
        result.ManufacturerName = ManufacturerIds.TryGetValue(mfrId, out var mn) ? mn : null;
        result.ProductCode      = data[10] | (data[11] << 8);

        // ── Manufacture date ───────────────────────────────────────────────
        uint serialBin = (uint)(data[12] | (data[13] << 8) | (data[14] << 16) | (data[15] << 24));
        byte week = data[16];
        int  year = data[17] + 1990;
        result.ManufactureWeek = (week != 0 && week != 0xFF) ? week : null;
        result.ManufactureYear = year;

        // ── Video input (byte 20) ──────────────────────────────────────────
        byte inputDef = data[20];
        if ((inputDef & 0x80) != 0)
        {
            int iface = inputDef & 0x0F;
            result.PanelType = DigitalInterface.TryGetValue(iface, out var pt) ? pt : "Digital";
        }
        else
        {
            result.PanelType = "Analog (VGA)";
        }

        // ── Physical size ──────────────────────────────────────────────────
        byte hCm = data[21], vCm = data[22];
        if (hCm > 0 && vCm > 0)
            result.DiagonalInches = Math.Round(Math.Sqrt(hCm * hCm + vCm * vCm) / 2.54, 1);

        // ── Chromaticity (bytes 25–34) ─────────────────────────────────────
        result.ColorRx = ChromaCoord(data[27], data[25], 6);
        result.ColorRy = ChromaCoord(data[28], data[25], 4);
        result.ColorGx = ChromaCoord(data[29], data[25], 2);
        result.ColorGy = ChromaCoord(data[30], data[25], 0);
        result.ColorBx = ChromaCoord(data[31], data[26], 6);
        result.ColorBy = ChromaCoord(data[32], data[26], 4);
        result.ColorWx = ChromaCoord(data[33], data[26], 2);
        result.ColorWy = ChromaCoord(data[34], data[26], 0);

        var primaries = new (double x, double y)[]
        {
            (result.ColorRx.Value, result.ColorRy.Value),
            (result.ColorGx.Value, result.ColorGy.Value),
            (result.ColorBx.Value, result.ColorBy.Value),
        };
        result.GamutSrgbPct     = CalculateGamutCoverage(primaries, SrgbPrimaries);
        result.GamutP3Pct       = CalculateGamutCoverage(primaries, DciP3Primaries);
        result.GamutAdobeRgbPct = CalculateGamutCoverage(primaries, AdobeRgbPrimaries);

        // ── Descriptor blocks (bytes 54–125) ──────────────────────────────
        string? serialNumber = null;
        foreach (int offset in new[] { 54, 72, 90, 108 })
        {
            if (offset + 18 > data.Length) continue;
            if (data[offset] == 0 && data[offset + 1] == 0 && data[offset + 2] == 0)
            {
                byte tag  = data[offset + 3];
                string text = System.Text.Encoding.Latin1
                    .GetString(data, offset + 5, 13)
                    .TrimEnd('\n', '\r', ' ', '\0');
                if (tag == DescMonitorName)
                    result.MonitorName = text;
                else if (tag == DescSerialNumber)
                    serialNumber = text;
                else if (tag == DescRangeLimits)
                {
                    result.MinRefreshHz = data[offset + 5];
                    result.MaxRefreshHz = data[offset + 6];
                }
            }
        }
        result.SerialNumber ??= serialNumber
            ?? (serialBin != 0 && serialBin != 0xFFFFFFFF ? serialBin.ToString() : null);

        // ── CTA-861 / DisplayID extension blocks (HDR metadata) ───────────
        for (int extOff = 128; extOff + 128 <= data.Length; extOff += 128)
        {
            byte extTag = data[extOff];
            if (extTag == 0x02) // CTA-861
            {
                int dtdOffset = data[extOff + 2];
                int end = Math.Min(dtdOffset, 126);
                ParseCtaBlocks(data, extOff + 4, end, result);
            }
            else if (extTag == 0x70) // DisplayID 2.x
            {
                int numBytes = data[extOff + 2];
                int pos = 5, end = Math.Min(5 + numBytes, 126);
                while (pos + 2 < end)
                {
                    byte blockType = data[extOff + pos];
                    int blockLen   = data[extOff + pos + 2];
                    if (pos + 3 + blockLen > end) break;
                    if (blockType == 0x81)
                        ParseCtaBlocks(data, extOff + pos + 3, pos + 3 + blockLen, result);
                    pos += 3 + blockLen;
                }
            }
        }

        return result;
    }

    private static void ParseCtaBlocks(byte[] data, int start, int length, DisplayInfo result)
    {
        int pos = start;
        int end = start + length;
        while (pos < end && pos < data.Length)
        {
            byte hdrByte = data[pos];
            int  tag     = (hdrByte >> 5) & 0x07;
            int  len     = hdrByte & 0x1F;
            if (pos + 1 + len > data.Length) break;
            if (tag == 7 && len > 0)
            {
                byte extTag = data[pos + 1];
                if (extTag == 6 && len > 1 && result.HdrTier is null)
                {
                    byte eotf = data[pos + 2];
                    var hdrs  = new List<string>();
                    if ((eotf & 0x04) != 0) hdrs.Add("HDR10");
                    if ((eotf & 0x08) != 0) hdrs.Add("HLG");
                    if (hdrs.Count > 0)
                    {
                        result.HdrTier = string.Join(", ", hdrs);
                        result.HdrSupported = true;
                    }
                    if (len > 3 && result.MaxLuminanceNits is null)
                    {
                        byte code = data[pos + 4];
                        result.MaxLuminanceNits = (int)(50 * Math.Pow(2, code / 32.0));
                    }
                }
            }
            pos += 1 + len;
        }
    }

    // ── Gamut coverage (Sutherland-Hodgman polygon clipping) ──────────────

    private static double PolygonArea(IList<(double x, double y)> vertices)
    {
        int n = vertices.Count;
        if (n < 3) return 0.0;
        double area = 0.0;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            area += vertices[i].x * vertices[j].y;
            area -= vertices[j].x * vertices[i].y;
        }
        return Math.Abs(area) * 0.5;
    }

    private static (double x, double y)? EdgeIntersect(
        (double x, double y) p1, (double x, double y) p2,
        (double x, double y) p3, (double x, double y) p4)
    {
        double denom = (p1.x - p2.x) * (p3.y - p4.y) - (p1.y - p2.y) * (p3.x - p4.x);
        if (Math.Abs(denom) < 1e-12) return null;
        double t = ((p1.x - p3.x) * (p3.y - p4.y) - (p1.y - p3.y) * (p3.x - p4.x)) / denom;
        return (p1.x + t * (p2.x - p1.x), p1.y + t * (p2.y - p1.y));
    }

    private static List<(double x, double y)> ClipByEdge(
        List<(double x, double y)> polygon,
        (double x, double y) eStart,
        (double x, double y) eEnd)
    {
        if (polygon.Count == 0) return [];

        bool Inside((double x, double y) pt)
        {
            double ex = eEnd.x - eStart.x;
            double ey = eEnd.y - eStart.y;
            return ex * (pt.y - eStart.y) - ey * (pt.x - eStart.x) >= 0;
        }

        var output = new List<(double x, double y)>();
        for (int i = 0; i < polygon.Count; i++)
        {
            var curr = polygon[i];
            var prev = polygon[(i - 1 + polygon.Count) % polygon.Count];
            if (Inside(curr))
            {
                if (!Inside(prev))
                {
                    var pt = EdgeIntersect(prev, curr, eStart, eEnd);
                    if (pt.HasValue) output.Add(pt.Value);
                }
                output.Add(curr);
            }
            else if (Inside(prev))
            {
                var pt = EdgeIntersect(prev, curr, eStart, eEnd);
                if (pt.HasValue) output.Add(pt.Value);
            }
        }
        return output;
    }

    /// <summary>
    /// Return the percentage of <paramref name="referencePrimaries"/> gamut
    /// covered by <paramref name="displayPrimaries"/>.
    /// Uses the Sutherland-Hodgman algorithm (same as display_info.py).
    /// </summary>
    public static double? CalculateGamutCoverage(
        (double x, double y)[] displayPrimaries,
        (double x, double y)[] referencePrimaries)
    {
        double refArea = PolygonArea(referencePrimaries);
        if (refArea < 1e-12) return null;

        var clipped = new List<(double x, double y)>(displayPrimaries);
        var refList = new List<(double x, double y)>(referencePrimaries);
        int n = refList.Count;
        for (int i = 0; i < n; i++)
        {
            clipped = ClipByEdge(clipped, refList[i], refList[(i + 1) % n]);
            if (clipped.Count == 0) return 0.0;
        }

        double intersection = PolygonArea(clipped);
        return Math.Min(100.0, intersection / refArea * 100.0);
    }

    // ── Scorecard helpers ─────────────────────────────────────────────────

    private static readonly double ArcminuteRad = Math.PI / 10800.0;

    /// <summary>Return the minimum PPI for a Retina Display at the given distance.</summary>
    public static double RetinaMinPpi(double distanceIn) =>
        1.0 / (distanceIn * Math.Tan(ArcminuteRad));

    /// <summary>Compute PPI from resolution and diagonal size.</summary>
    public static double CalculatePpi(int w, int h, double diagonalIn) =>
        Math.Sqrt(w * (double)w + h * (double)h) / diagonalIn;

    /// <summary>Map a 0–100 percentage to a letter grade.</summary>
    public static string ScoreLabel(double pct) => pct switch
    {
        >= 90 => "A",
        >= 75 => "B",
        >= 60 => "C",
        >= 40 => "D",
        _     => "F",
    };

    private static readonly Color Green  = Color.FromArgb("#007a00");
    private static readonly Color Orange = Color.FromArgb("#b85c00");
    private static readonly Color Red    = Color.FromArgb("#cc0000");
    private static readonly Color Gray   = Color.FromArgb("#555555");

    public static Color ScoreColor(double pct) => pct switch
    {
        >= 75 => Green,
        >= 50 => Orange,
        _     => Red,
    };

    public static Color ResultColor(string result) => result switch
    {
        "PASS"   => Green,
        "REVIEW" => Orange,
        "FAIL"   => Red,
        _        => Gray,
    };

    /// <summary>
    /// Build the four-row display scorecard from a <see cref="DisplayInfo"/>
    /// and the user's selected viewing distance.
    /// </summary>
    public static (List<DisplayScorecardRow> Rows, string OverallGrade,
                   string OverallDesc, Color OverallColor)
        BuildScorecard(DisplayInfo info, int? screenW, int? screenH,
                       double diagonalIn, double viewingDistanceIn)
    {
        var rows   = new List<DisplayScorecardRow>();
        var scores = new List<double>();

        double minPpi = RetinaMinPpi(viewingDistanceIn);

        // --- PPI ---
        double? ppi = null;
        if (diagonalIn > 0 && screenW.HasValue && screenH.HasValue &&
            screenW > 0 && screenH > 0)
        {
            ppi = CalculatePpi(screenW.Value, screenH.Value, diagonalIn);
        }

        rows.Add(new DisplayScorecardRow
        {
            Metric  = "Pixel Density (PPI)",
            Value   = ppi.HasValue ? $"{ppi.Value:F1} PPI" : "—",
            Target  = $">= {minPpi:F0} PPI @ {viewingDistanceIn:F0} in",
            Result  = ppi.HasValue ? (ppi.Value >= minPpi ? "PASS" : "FAIL") : "—",
            ResultColor = ppi.HasValue
                ? ResultColor(ppi.Value >= minPpi ? "PASS" : "FAIL")
                : Gray,
        });
        if (ppi.HasValue) scores.Add(Math.Min(ppi.Value / minPpi * 100.0, 100.0));

        // --- DCI-P3 ---
        double p3Target = 95.0;
        double? p3 = info.GamutP3Pct;
        string p3Result = p3.HasValue
            ? (p3 >= p3Target ? "PASS" : p3 >= 80 ? "REVIEW" : "FAIL")
            : "—";
        rows.Add(new DisplayScorecardRow
        {
            Metric      = "DCI-P3 Gamut",
            Value       = p3.HasValue ? $"{p3.Value:F1}%" : "Unknown",
            Target      = $">= {p3Target:F0}%",
            Result      = p3Result,
            ResultColor = ResultColor(p3Result),
        });
        if (p3.HasValue) scores.Add(Math.Min(p3.Value / p3Target * 100.0, 100.0));

        // --- Refresh Rate ---
        double tgtHz = 120;
        double? rr = info.RefreshRate ?? info.MaxRefreshHz;
        string rrResult = rr.HasValue
            ? (rr >= tgtHz ? "PASS" : rr >= 60 ? "REVIEW" : "FAIL")
            : "—";
        rows.Add(new DisplayScorecardRow
        {
            Metric      = "Refresh Rate",
            Value       = rr.HasValue ? $"{rr.Value:F0} Hz" : "Unknown",
            Target      = $">= {tgtHz:F0} Hz",
            Result      = rrResult,
            ResultColor = ResultColor(rrResult),
        });
        if (rr.HasValue) scores.Add(Math.Min(rr.Value / tgtHz * 100.0, 100.0));

        // --- HDR ---
        bool hasHdr = !string.IsNullOrEmpty(info.HdrTier);
        rows.Add(new DisplayScorecardRow
        {
            Metric      = "HDR Support",
            Value       = hasHdr ? info.HdrTier! : "Not detected",
            Target      = "HDR supported",
            Result      = hasHdr ? "PASS" : "FAIL",
            ResultColor = ResultColor(hasHdr ? "PASS" : "FAIL"),
        });
        scores.Add(hasHdr ? 100.0 : 0.0);

        // --- Overall ---
        string grade, desc;
        Color color;
        if (scores.Count > 0)
        {
            double overall = scores.Average();
            grade = ScoreLabel(overall);
            color = ScoreColor(overall);
            desc = overall switch
            {
                >= 90 => "Excellent — meets or exceeds Apple Retina standards",
                >= 75 => "Good — close to Apple Retina quality",
                >= 50 => "Fair — some areas fall short of Retina standards",
                _     => "Below Apple Retina standards",
            };
        }
        else
        {
            grade = "—";
            desc  = "Enter screen diagonal to get your score";
            color = Gray;
        }

        return (rows, grade, desc, color);
    }
}
