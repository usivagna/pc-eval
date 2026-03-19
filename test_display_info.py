"""Unit tests for display_info.py core logic (no hardware or OS required)."""

import struct
import unittest

from display_info import (
    ADOBE_RGB_PRIMARIES,
    APPLE_TARGETS,
    DCI_P3_PRIMARIES,
    SRGB_PRIMARIES,
    _EDID_HEADER,
    _chroma_coord,
    _clip_by_edge,
    _edge_intersect,
    _polygon_area,
    calculate_gamut_coverage,
    decode_manufacturer_id,
    get_all_displays_info,
    get_display_info,
    parse_edid,
    scorecard,
)


# ---------------------------------------------------------------------------
# Helpers for building synthetic EDID blobs
# ---------------------------------------------------------------------------

def _build_edid(
    manufacturer_id: str = "DEL",
    product_code: int = 0x1234,
    serial_bin: int = 0,
    week: int = 14,
    year_offset: int = 33,      # 1990 + 33 = 2023
    input_def: int = 0x85,      # digital, DisplayPort
    chroma_msbs: bytes = bytes(8),
    chroma_lsbs_25_26: bytes = bytes(2),
    descriptors: bytes = bytes(72),
) -> bytes:
    """Build a minimal 128-byte EDID blob for testing."""
    edid = bytearray(128)
    edid[0:8]   = _EDID_HEADER
    # Manufacturer ID
    c1, c2, c3 = (ord(c) - 64 for c in manufacturer_id.upper())
    raw_mfr = (c1 << 10) | (c2 << 5) | c3
    edid[8:10]  = struct.pack(">H", raw_mfr)
    edid[10:12] = struct.pack("<H", product_code)
    edid[12:16] = struct.pack("<I", serial_bin)
    edid[16]    = week
    edid[17]    = year_offset
    edid[20]    = input_def
    # Colour characteristics LSBs (bytes 25–26) and MSBs (bytes 27–34)
    edid[25:27] = chroma_lsbs_25_26
    edid[27:35] = chroma_msbs
    # Descriptor blocks (bytes 54–125)
    edid[54:126] = descriptors
    return bytes(edid)


def _make_monitor_name_descriptor(name: str) -> bytes:
    """Return an 18-byte EDID monitor-name descriptor."""
    raw = bytearray(18)
    raw[0:4] = b"\x00\x00\x00\xfc"
    raw[4]   = 0
    text_bytes = name.encode("latin-1")[:13]
    raw[5: 5 + len(text_bytes)] = text_bytes
    if len(text_bytes) < 13:
        raw[5 + len(text_bytes)] = ord("\n")
    return bytes(raw)


def _make_serial_descriptor(serial: str) -> bytes:
    """Return an 18-byte EDID serial-number descriptor."""
    raw = bytearray(18)
    raw[0:4] = b"\x00\x00\x00\xff"
    raw[4]   = 0
    text_bytes = serial.encode("latin-1")[:13]
    raw[5: 5 + len(text_bytes)] = text_bytes
    if len(text_bytes) < 13:
        raw[5 + len(text_bytes)] = ord("\n")
    return bytes(raw)


def _make_range_limits_descriptor(min_hz: int, max_hz: int) -> bytes:
    """Return an 18-byte EDID range-limits descriptor."""
    raw = bytearray(18)
    raw[0:4] = b"\x00\x00\x00\xfd"
    raw[4]   = 0
    raw[5]   = min_hz
    raw[6]   = max_hz
    return bytes(raw)


# ---------------------------------------------------------------------------
# Tests: decode_manufacturer_id
# ---------------------------------------------------------------------------

class TestDecodeManufacturerId(unittest.TestCase):

    def _encode(self, code: str) -> int:
        c1, c2, c3 = (ord(c) - 64 for c in code.upper())
        return (c1 << 10) | (c2 << 5) | c3

    def test_apple(self):
        self.assertEqual(decode_manufacturer_id(self._encode("APP")), "APP")

    def test_dell(self):
        self.assertEqual(decode_manufacturer_id(self._encode("DEL")), "DEL")

    def test_samsung(self):
        self.assertEqual(decode_manufacturer_id(self._encode("SAM")), "SAM")

    def test_roundtrip_various(self):
        for code in ("LEN", "BOE", "AUO", "HWP"):
            with self.subTest(code=code):
                self.assertEqual(
                    decode_manufacturer_id(self._encode(code)), code
                )


# ---------------------------------------------------------------------------
# Tests: _chroma_coord
# ---------------------------------------------------------------------------

class TestChromaCoord(unittest.TestCase):

    def test_all_zeros(self):
        self.assertAlmostEqual(_chroma_coord(0, 0, 6), 0.0)

    def test_max_value(self):
        # MSB = 255, LSBs = 0b11
        result = _chroma_coord(255, 0b11 << 6, 6)
        self.assertAlmostEqual(result, 1023 / 1024)

    def test_shift_variants(self):
        # LSBs at various bit positions
        lsb_byte = 0b00110000  # bits 5:4 = 0b11
        result = _chroma_coord(0, lsb_byte, 4)
        self.assertAlmostEqual(result, 3 / 1024)

    def test_known_srgb_red_x(self):
        # sRGB red x ≈ 0.64, encoded as round(0.64 * 1024) = 655 = 0x28F
        # MSB = 0x28F >> 2 = 0xA3 = 163
        # LSBs = 0x28F & 0x03 = 3  → placed at shift 6 → 0b11_000000 = 0xC0
        msb = 163
        lsb_byte = 0xC0
        result = _chroma_coord(msb, lsb_byte, 6)
        self.assertAlmostEqual(result, 0.64, delta=0.002)


# ---------------------------------------------------------------------------
# Tests: parse_edid
# ---------------------------------------------------------------------------

class TestParseEdid(unittest.TestCase):

    def test_empty_returns_nones(self):
        result = parse_edid(b"")
        self.assertIsNone(result["manufacturer_id"])

    def test_bad_header_returns_nones(self):
        result = parse_edid(bytes(128))
        self.assertIsNone(result["manufacturer_id"])

    def test_manufacturer_id_decoded(self):
        edid = _build_edid(manufacturer_id="DEL")
        result = parse_edid(edid)
        self.assertEqual(result["manufacturer_id"], "DEL")

    def test_manufacturer_name_resolved(self):
        edid = _build_edid(manufacturer_id="APP")
        result = parse_edid(edid)
        self.assertEqual(result["manufacturer_name"], "Apple")

    def test_unknown_manufacturer_name_is_none(self):
        edid = _build_edid(manufacturer_id="XYZ")
        result = parse_edid(edid)
        self.assertIsNone(result["manufacturer_name"])

    def test_product_code(self):
        edid = _build_edid(product_code=0xABCD)
        result = parse_edid(edid)
        self.assertEqual(result["product_code"], 0xABCD)

    def test_manufacture_year(self):
        # year_offset=33 → 1990+33=2023
        edid = _build_edid(year_offset=33)
        result = parse_edid(edid)
        self.assertEqual(result["manufacture_year"], 2023)

    def test_manufacture_week(self):
        edid = _build_edid(week=22)
        result = parse_edid(edid)
        self.assertEqual(result["manufacture_week"], 22)

    def test_manufacture_week_zero_is_none(self):
        edid = _build_edid(week=0)
        result = parse_edid(edid)
        self.assertIsNone(result["manufacture_week"])

    def test_panel_type_displayport(self):
        # input_def = 0b10000101 = digital + DP (0x05)
        edid = _build_edid(input_def=0x85)
        result = parse_edid(edid)
        self.assertEqual(result["panel_type"], "DisplayPort")

    def test_panel_type_hdmi(self):
        edid = _build_edid(input_def=0x82)
        result = parse_edid(edid)
        self.assertEqual(result["panel_type"], "HDMI-a")

    def test_panel_type_analog(self):
        edid = _build_edid(input_def=0x00)
        result = parse_edid(edid)
        self.assertEqual(result["panel_type"], "Analog (VGA)")

    def test_monitor_name_descriptor(self):
        # Descriptor text field is 13 bytes; "Dell S2722QC" is 12 chars → fits
        name_desc  = _make_monitor_name_descriptor("Dell S2722QC")
        padding    = bytes(72 - len(name_desc))
        descriptors = name_desc + padding
        edid = _build_edid(descriptors=descriptors)
        result = parse_edid(edid)
        self.assertEqual(result["monitor_name"], "Dell S2722QC")

    def test_serial_descriptor(self):
        serial_desc = _make_serial_descriptor("SN12345678")
        padding     = bytes(72 - len(serial_desc))
        descriptors = serial_desc + padding
        edid = _build_edid(descriptors=descriptors)
        result = parse_edid(edid)
        self.assertEqual(result["serial_number"], "SN12345678")

    def test_range_limits_descriptor(self):
        range_desc  = _make_range_limits_descriptor(48, 144)
        padding     = bytes(72 - len(range_desc))
        descriptors = range_desc + padding
        edid = _build_edid(descriptors=descriptors)
        result = parse_edid(edid)
        self.assertEqual(result["min_refresh_hz"], 48)
        self.assertEqual(result["max_refresh_hz"], 144)

    def test_binary_serial_fallback(self):
        """When no ASCII serial descriptor is present, use binary serial."""
        edid = _build_edid(serial_bin=987654321, descriptors=bytes(72))
        result = parse_edid(edid)
        self.assertEqual(result["serial_number"], "987654321")

    def test_binary_serial_zero_is_none(self):
        edid = _build_edid(serial_bin=0, descriptors=bytes(72))
        result = parse_edid(edid)
        self.assertIsNone(result["serial_number"])

    def test_chromaticity_zero_bytes(self):
        """All-zero chroma bytes → all coordinates are 0.0."""
        edid = _build_edid(chroma_msbs=bytes(8), chroma_lsbs_25_26=bytes(2))
        result = parse_edid(edid)
        for key in ("color_rx", "color_ry", "color_gx", "color_gy",
                    "color_bx", "color_by", "color_wx", "color_wy"):
            self.assertAlmostEqual(result[key], 0.0, msg=f"{key} should be 0")

    def test_gamut_values_present_for_nonzero_chroma(self):
        """Non-zero chromaticity → gamut percentages are computed."""
        # Encode approximate sRGB primaries
        # Rx≈0.64 → 0.64*1024=655=0x28F; MSB=163, LSB=3 (shift 6 → 0xC0)
        # Ry≈0.33 → 0.33*1024=338=0x152; MSB=84,  LSB=2 (shift 4 → 0x20)
        # Gx≈0.30 → 0.30*1024=307=0x133; MSB=76,  LSB=3 (shift 2 → 0x0C)
        # Gy≈0.60 → 0.60*1024=614=0x266; MSB=153, LSB=2 (shift 0 → 0x02)
        # Bx≈0.15 → 0.15*1024=153=0x099; MSB=38,  LSB=1 (shift 6 → 0x40)
        # By≈0.06 → 0.06*1024=61 =0x03D; MSB=15,  LSB=1 (shift 4 → 0x10)
        # Wx≈0.3127→0.3127*1024=320=0x140;MSB=80, LSB=0 (shift 2 → 0x00)
        # Wy≈0.3290→0.3290*1024=337=0x151;MSB=84, LSB=1 (shift 0 → 0x01)
        msbs = bytes([163, 84, 76, 153, 38, 15, 80, 84])
        lsbs_25 = (3 << 6) | (2 << 4) | (3 << 2) | (2 << 0)   # Rx,Ry,Gx,Gy
        lsbs_26 = (1 << 6) | (1 << 4) | (0 << 2) | (1 << 0)   # Bx,By,Wx,Wy
        edid = _build_edid(
            chroma_msbs=msbs,
            chroma_lsbs_25_26=bytes([lsbs_25, lsbs_26]),
        )
        result = parse_edid(edid)
        # sRGB primaries should yield ~100% sRGB coverage
        self.assertIsNotNone(result["gamut_srgb_pct"])
        self.assertIsNotNone(result["gamut_p3_pct"])
        self.assertIsNotNone(result["gamut_adobergb_pct"])
        self.assertGreater(result["gamut_srgb_pct"], 95.0)


# ---------------------------------------------------------------------------
# Tests: _polygon_area
# ---------------------------------------------------------------------------

class TestPolygonArea(unittest.TestCase):

    def test_unit_square(self):
        square = [(0, 0), (1, 0), (1, 1), (0, 1)]
        self.assertAlmostEqual(_polygon_area(square), 1.0)

    def test_right_triangle(self):
        tri = [(0, 0), (2, 0), (0, 2)]
        self.assertAlmostEqual(_polygon_area(tri), 2.0)

    def test_degenerate_line(self):
        line = [(0, 0), (1, 0), (2, 0)]
        self.assertAlmostEqual(_polygon_area(line), 0.0)

    def test_empty(self):
        self.assertAlmostEqual(_polygon_area([]), 0.0)

    def test_two_points(self):
        self.assertAlmostEqual(_polygon_area([(0, 0), (1, 1)]), 0.0)


# ---------------------------------------------------------------------------
# Tests: _edge_intersect
# ---------------------------------------------------------------------------

class TestEdgeIntersect(unittest.TestCase):

    def test_perpendicular_lines(self):
        pt = _edge_intersect((0, 1), (2, 1), (1, 0), (1, 2))
        self.assertIsNotNone(pt)
        self.assertAlmostEqual(pt[0], 1.0)
        self.assertAlmostEqual(pt[1], 1.0)

    def test_parallel_lines_return_none(self):
        pt = _edge_intersect((0, 0), (1, 0), (0, 1), (1, 1))
        self.assertIsNone(pt)

    def test_coincident_lines_return_none(self):
        pt = _edge_intersect((0, 0), (1, 0), (0, 0), (1, 0))
        self.assertIsNone(pt)


# ---------------------------------------------------------------------------
# Tests: _clip_by_edge
# ---------------------------------------------------------------------------

class TestClipByEdge(unittest.TestCase):

    def test_all_inside(self):
        """Square fully inside the left half-plane of a vertical edge."""
        square = [(0.0, 0.0), (0.5, 0.0), (0.5, 0.5), (0.0, 0.5)]
        # Edge from (1,0)→(1,1) — all points are to the left
        result = _clip_by_edge(square, (1.0, 0.0), (1.0, 1.0))
        self.assertEqual(len(result), 4)

    def test_all_outside(self):
        """Square fully outside."""
        square = [(2.0, 0.0), (3.0, 0.0), (3.0, 1.0), (2.0, 1.0)]
        result = _clip_by_edge(square, (1.0, 0.0), (1.0, 1.0))
        self.assertEqual(len(result), 0)

    def test_partial_clip(self):
        """Square partially clipped; result should be a rectangle (4 verts)."""
        square = [(0.5, 0.0), (1.5, 0.0), (1.5, 1.0), (0.5, 1.0)]
        result = _clip_by_edge(square, (1.0, 0.0), (1.0, 1.0))
        # Two outside corners replaced by two intersection points → still 4 verts
        self.assertGreater(len(result), 0)
        self.assertLessEqual(len(result), 4)
        # All remaining points must be at x ≤ 1.0
        for x, _y in result:
            self.assertLessEqual(x, 1.0 + 1e-9)

    def test_empty_polygon(self):
        self.assertEqual(_clip_by_edge([], (0.0, 0.0), (1.0, 0.0)), [])


# ---------------------------------------------------------------------------
# Tests: calculate_gamut_coverage
# ---------------------------------------------------------------------------

class TestCalculateGamutCoverage(unittest.TestCase):

    def test_identical_triangles_100_percent(self):
        """A triangle covers 100% of itself."""
        pct = calculate_gamut_coverage(SRGB_PRIMARIES, SRGB_PRIMARIES)
        self.assertIsNotNone(pct)
        self.assertAlmostEqual(pct, 100.0, delta=0.1)

    def test_p3_larger_than_srgb(self):
        """DCI-P3 covers more area than sRGB; coverage of sRGB should be 100%."""
        pct = calculate_gamut_coverage(DCI_P3_PRIMARIES, SRGB_PRIMARIES)
        self.assertIsNotNone(pct)
        self.assertAlmostEqual(pct, 100.0, delta=1.0)

    def test_srgb_vs_p3_less_than_100(self):
        """sRGB covers less than 100% of DCI-P3."""
        pct = calculate_gamut_coverage(SRGB_PRIMARIES, DCI_P3_PRIMARIES)
        self.assertIsNotNone(pct)
        self.assertLess(pct, 100.0)
        self.assertGreater(pct, 50.0)

    def test_zero_reference_area_returns_none(self):
        """A degenerate (zero-area) reference triangle should return None."""
        degenerate = ((0.0, 0.0), (0.5, 0.0), (1.0, 0.0))  # collinear
        pct = calculate_gamut_coverage(SRGB_PRIMARIES, degenerate)
        self.assertIsNone(pct)

    def test_non_overlapping_triangles_zero(self):
        """Two non-overlapping triangles → 0% coverage."""
        far_triangle = ((10.0, 0.0), (11.0, 0.0), (10.5, 1.0))
        pct = calculate_gamut_coverage(SRGB_PRIMARIES, far_triangle)
        self.assertIsNotNone(pct)
        self.assertAlmostEqual(pct, 0.0, delta=0.001)

    def test_result_capped_at_100(self):
        """Result must never exceed 100."""
        pct = calculate_gamut_coverage(DCI_P3_PRIMARIES, SRGB_PRIMARIES)
        self.assertLessEqual(pct, 100.0)

    def test_result_non_negative(self):
        pct = calculate_gamut_coverage(SRGB_PRIMARIES, ADOBE_RGB_PRIMARIES)
        self.assertIsNotNone(pct)
        self.assertGreaterEqual(pct, 0.0)

    def test_adobe_rgb_vs_srgb_coverage(self):
        """Adobe RGB is wider than sRGB; sRGB covers less than 100% of Adobe RGB."""
        pct = calculate_gamut_coverage(SRGB_PRIMARIES, ADOBE_RGB_PRIMARIES)
        self.assertIsNotNone(pct)
        self.assertLess(pct, 100.0)
        self.assertGreater(pct, 60.0)


# ---------------------------------------------------------------------------
# Tests: get_display_info
# ---------------------------------------------------------------------------

class TestGetDisplayInfo(unittest.TestCase):

    def test_returns_dict(self):
        info = get_display_info()
        self.assertIsInstance(info, dict)

    def test_contains_platform_key(self):
        info = get_display_info()
        self.assertIn("platform", info)
        self.assertIsInstance(info["platform"], str)

    def test_expected_keys_present(self):
        info = get_display_info()
        expected_keys = [
            "resolution_width", "resolution_height", "refresh_rate",
            "adaptive_sync", "hdr_tier", "gamut_srgb_pct", "gamut_p3_pct",
            "gamut_adobergb_pct", "manufacturer_id", "monitor_name",
            "serial_number", "icc_profile_name",
        ]
        for key in expected_keys:
            with self.subTest(key=key):
                self.assertIn(key, info)

    def test_numeric_fields_are_numeric_or_none(self):
        info = get_display_info()
        for key in ("resolution_width", "resolution_height", "refresh_rate",
                    "gamut_srgb_pct", "gamut_p3_pct"):
            val = info.get(key)
            if val is not None:
                with self.subTest(key=key):
                    self.assertIsInstance(val, (int, float))


# ---------------------------------------------------------------------------
# Tests: get_all_displays_info
# ---------------------------------------------------------------------------

class TestGetAllDisplaysInfo(unittest.TestCase):

    def test_returns_list(self):
        displays = get_all_displays_info()
        self.assertIsInstance(displays, list)

    def test_non_empty(self):
        displays = get_all_displays_info()
        self.assertGreater(len(displays), 0)

    def test_each_entry_is_dict(self):
        for info in get_all_displays_info():
            with self.subTest(info=info.get("monitor_name")):
                self.assertIsInstance(info, dict)

    def test_each_entry_has_platform_key(self):
        for info in get_all_displays_info():
            with self.subTest(info=info.get("monitor_name")):
                self.assertIn("platform", info)
                self.assertIsInstance(info["platform"], str)

    def test_each_entry_has_expected_keys(self):
        expected_keys = [
            "resolution_width", "resolution_height", "refresh_rate",
            "adaptive_sync", "hdr_tier", "gamut_srgb_pct", "gamut_p3_pct",
            "gamut_adobergb_pct", "manufacturer_id", "monitor_name",
            "serial_number", "icc_profile_name", "diagonal_inches",
        ]
        for info in get_all_displays_info():
            for key in expected_keys:
                with self.subTest(monitor=info.get("monitor_name"), key=key):
                    self.assertIn(key, info)

    def test_numeric_fields_are_numeric_or_none(self):
        for info in get_all_displays_info():
            for key in ("resolution_width", "resolution_height", "refresh_rate",
                        "gamut_srgb_pct", "gamut_p3_pct", "gamut_adobergb_pct",
                        "diagonal_inches"):
                val = info.get(key)
                if val is not None:
                    with self.subTest(monitor=info.get("monitor_name"), key=key):
                        self.assertIsInstance(val, (int, float))

    def test_first_entry_matches_get_display_info_platform(self):
        """Primary display platform must match get_display_info() platform."""
        single = get_display_info()
        all_displays = get_all_displays_info()
        self.assertEqual(all_displays[0]["platform"], single["platform"])


# ---------------------------------------------------------------------------
# Tests: scorecard
# ---------------------------------------------------------------------------

class TestScorecard(unittest.TestCase):

    def _minimal_info(self, **kwargs) -> dict:
        base = {
            "platform": "Darwin",
            "resolution_width": None,
            "resolution_height": None,
            "refresh_rate": None,
            "min_refresh_hz": None,
            "max_refresh_hz": None,
            "adaptive_sync": None,
            "adaptive_sync_range": None,
            "hdr_tier": None,
            "hdr_active": None,
            "gamut_p3_pct": None,
            "icc_profile_name": None,
            "icc_profile_path": None,
        }
        base.update(kwargs)
        return base

    def test_returns_list(self):
        rows = scorecard(self._minimal_info())
        self.assertIsInstance(rows, list)
        self.assertGreater(len(rows), 0)

    def test_each_row_has_required_keys(self):
        rows = scorecard(self._minimal_info())
        for row in rows:
            for key in ("metric", "value", "target", "result", "note"):
                with self.subTest(row=row["metric"], key=key):
                    self.assertIn(key, row)

    def test_p3_pass(self):
        rows = scorecard(self._minimal_info(gamut_p3_pct=96.0))
        p3_row = next(r for r in rows if "P3" in r["metric"])
        self.assertEqual(p3_row["result"], "PASS")

    def test_p3_review(self):
        rows = scorecard(self._minimal_info(gamut_p3_pct=92.0))
        p3_row = next(r for r in rows if "P3" in r["metric"])
        self.assertEqual(p3_row["result"], "REVIEW")

    def test_p3_fail(self):
        rows = scorecard(self._minimal_info(gamut_p3_pct=70.0))
        p3_row = next(r for r in rows if "P3" in r["metric"])
        self.assertEqual(p3_row["result"], "FAIL")

    def test_refresh_pass(self):
        rows = scorecard(self._minimal_info(
            refresh_rate=120.0, min_refresh_hz=1, max_refresh_hz=120
        ))
        rr_row = next(r for r in rows if "Refresh" in r["metric"])
        self.assertEqual(rr_row["result"], "PASS")

    def test_hardware_stubs_present(self):
        """Scorecard must include Hardware Verified placeholder rows."""
        rows = scorecard(self._minimal_info())
        stub_rows = [r for r in rows if "colorimeter" in r["note"]]
        self.assertGreaterEqual(len(stub_rows), 4)

    def test_hardware_stubs_result_is_dash(self):
        rows = scorecard(self._minimal_info())
        for row in rows:
            if "colorimeter" in row["note"]:
                self.assertEqual(row["result"], "—")

    def test_apple_targets_values(self):
        """Confirm expected numeric targets are in place."""
        self.assertEqual(APPLE_TARGETS["ppi_macbook"], 218)
        self.assertEqual(APPLE_TARGETS["ppi_iphone"], 254)
        self.assertEqual(APPLE_TARGETS["p3_pct_min"], 95.0)
        self.assertEqual(APPLE_TARGETS["refresh_min_hz"], 1)
        self.assertEqual(APPLE_TARGETS["refresh_max_hz"], 120)


if __name__ == "__main__":
    unittest.main()
