"""
Display Information Collection
===============================
Collects self-reported / unverified display data from the operating system and
EDID.  **None of the values returned here have been independently verified with
calibration hardware.**  Every returned value originates from manufacturer
declarations or OS APIs and should be treated as indicative, not authoritative.

Public API
----------
``get_display_info()``         – Return a ``DisplayInfo`` dict for the primary display.
``get_all_displays_info()``    – Return a list of ``DisplayInfo`` dicts, one per
                                 connected display (primary first).
``parse_edid(data)``           – Parse a raw EDID binary blob into a structured dict.
``calculate_gamut_coverage(primaries, reference)``
                               – Return the % of *reference* gamut covered by *primaries*.
``scorecard(info)``            – Compare ``DisplayInfo`` against Apple reference targets.
"""

from __future__ import annotations

import json
import math
import os
import platform
import struct
import subprocess
import sys
from typing import Any, Dict, List, Optional, Sequence, Tuple

# ---------------------------------------------------------------------------
# Type alias
# ---------------------------------------------------------------------------

# (x, y) CIE 1931 chromaticity coordinate
_Chroma = Tuple[float, float]
# Triangle of three primaries in CIE xy
_Primaries = Tuple[_Chroma, _Chroma, _Chroma]

# ---------------------------------------------------------------------------
# Standard colour-space primaries (CIE xy)
# ---------------------------------------------------------------------------

SRGB_PRIMARIES: _Primaries = ((0.640, 0.330), (0.300, 0.600), (0.150, 0.060))
DCI_P3_PRIMARIES: _Primaries = ((0.680, 0.320), (0.265, 0.690), (0.150, 0.060))
ADOBE_RGB_PRIMARIES: _Primaries = ((0.640, 0.330), (0.210, 0.710), (0.150, 0.060))

# ---------------------------------------------------------------------------
# Apple scorecard reference targets (from issue #2)
# ---------------------------------------------------------------------------

APPLE_TARGETS = {
    "ppi_macbook":     218,    # minimum PPI for MacBook-class Retina
    "ppi_iphone":      254,    # minimum PPI for iPhone-class Retina
    "p3_pct_min":       95.0,  # reported DCI-P3 gamut coverage target
    "hdr_tier":        "DisplayHDR 1000",
    "refresh_min_hz":   1,
    "refresh_max_hz": 120,
}

# ---------------------------------------------------------------------------
# EDID parsing
# ---------------------------------------------------------------------------

_EDID_HEADER = b"\x00\xff\xff\xff\xff\xff\xff\x00"

# Descriptor block tag bytes
_DESC_SERIAL_NUMBER  = 0xFF
_DESC_MONITOR_NAME   = 0xFC
_DESC_RANGE_LIMITS   = 0xFD

# Abbreviated list of EDID manufacturer IDs → display brand names
_MANUFACTURER_IDS: Dict[str, str] = {
    "APP": "Apple",
    "SAM": "Samsung Electronics",
    "SDC": "Samsung Display",
    "LEN": "Lenovo",
    "DEL": "Dell",
    "LGD": "LG Display",
    "LGE": "LG Electronics",
    "AUO": "AU Optronics",
    "CMN": "Chimei Innolux",
    "BOE": "BOE Technology",
    "SHP": "Sharp",
    "IVO": "InfoVision Optoelectronics",
    "HWP": "Hewlett-Packard",
    "ACR": "Acer",
    "NEC": "NEC Display Solutions",
    "VSC": "ViewSonic",
    "BNQ": "BenQ",
    "MSI": "MSI",
    "AOC": "AOC International",
    "ASU": "ASUS",
    "AUS": "ASUS",
    "PHI": "Philips",
    "EIZ": "EIZO",
    "PHL": "Philips Consumer Electronics",
    "SNY": "Sony",
}

# Digital interface sub-type codes (EDID 1.4, byte 20 bits 3:0)
_DIGITAL_INTERFACE: Dict[int, str] = {
    0x00: "Undefined",
    0x01: "DVI",
    0x02: "HDMI-a",
    0x03: "HDMI-b",
    0x04: "MDDI",
    0x05: "DisplayPort",
}


def decode_manufacturer_id(raw: int) -> str:
    """Decode a 2-byte big-endian EDID manufacturer field to a 3-letter code.

    Each letter is stored as a 5-bit value offset from ASCII 'A' (= 1).

    Args:
        raw: 16-bit unsigned integer read big-endian from EDID bytes 8–9.

    Returns:
        Three-character manufacturer code, e.g. ``"APP"``, ``"DEL"``.
    """
    c1 = ((raw >> 10) & 0x1F) + 64
    c2 = ((raw >> 5)  & 0x1F) + 64
    c3 = ((raw)       & 0x1F) + 64
    return chr(c1) + chr(c2) + chr(c3)


def _chroma_coord(msb_byte: int, lsb_byte: int, lsb_shift: int) -> float:
    """Build a 10-bit chromaticity value and normalise to 0.0–1.0.

    Args:
        msb_byte:  8 most-significant bits of the coordinate.
        lsb_byte:  Byte containing the 2 least-significant bits.
        lsb_shift: Bit position (0-6, even) of the 2 LSBs within *lsb_byte*.

    Returns:
        Normalised CIE xy coordinate.
    """
    lsb = (lsb_byte >> lsb_shift) & 0x03
    return ((msb_byte << 2) | lsb) / 1024.0


def parse_edid(data: bytes) -> Dict[str, Any]:
    """Parse a raw EDID binary blob (128 bytes or longer).

    Only the base 128-byte block is processed; CEA/DisplayID extensions are
    noted but not decoded in this version.

    Args:
        data: Raw EDID bytes.

    Returns:
        Dict with the following keys (values are ``None`` when unavailable):

        ``manufacturer_id``, ``manufacturer_name``, ``product_code``,
        ``serial_number``, ``manufacture_week``, ``manufacture_year``,
        ``monitor_name``, ``panel_type``, ``min_refresh_hz``,
        ``max_refresh_hz``, ``color_rx``, ``color_ry``, ``color_gx``,
        ``color_gy``, ``color_bx``, ``color_by``, ``color_wx``, ``color_wy``,
        ``gamut_srgb_pct``, ``gamut_p3_pct``, ``gamut_adobergb_pct``.
    """
    result: Dict[str, Any] = {
        "manufacturer_id":    None,
        "manufacturer_name":  None,
        "product_code":       None,
        "serial_number":      None,
        "manufacture_week":   None,
        "manufacture_year":   None,
        "monitor_name":       None,
        "panel_type":         None,
        "min_refresh_hz":     None,
        "max_refresh_hz":     None,
        "color_rx": None, "color_ry": None,
        "color_gx": None, "color_gy": None,
        "color_bx": None, "color_by": None,
        "color_wx": None, "color_wy": None,
        "gamut_srgb_pct":     None,
        "gamut_p3_pct":       None,
        "gamut_adobergb_pct": None,
        "hdr_supported":      None,
        "max_luminance_nits": None,
    }

    if len(data) < 128 or data[:8] != _EDID_HEADER:
        return result

    # ── Manufacturer & product ────────────────────────────────────────────
    mfr_raw = struct.unpack(">H", data[8:10])[0]
    mfr_id = decode_manufacturer_id(mfr_raw)
    result["manufacturer_id"]   = mfr_id
    result["manufacturer_name"] = _MANUFACTURER_IDS.get(mfr_id)
    result["product_code"]      = struct.unpack("<H", data[10:12])[0]

    # ── Manufacture date ──────────────────────────────────────────────────
    serial_bin = struct.unpack("<I", data[12:16])[0]
    week = data[16]
    year = data[17] + 1990
    result["manufacture_week"] = int(week) if week not in (0, 0xFF) else None
    result["manufacture_year"] = int(year)

    # ── Video input definition (byte 20) ──────────────────────────────────
    input_def = data[20]
    if input_def & 0x80:  # bit 7 = 1 → digital
        iface = input_def & 0x0F
        result["panel_type"] = _DIGITAL_INTERFACE.get(iface, "Digital")
    else:
        result["panel_type"] = "Analog (VGA)"

    # ── Chromaticity coordinates (bytes 25–34) ────────────────────────────
    # Byte 25: Rx[1:0] Ry[1:0] Gx[1:0] Gy[1:0]
    # Byte 26: Bx[1:0] By[1:0] Wx[1:0] Wy[1:0]
    # Bytes 27–34: MSBs for Rx Ry Gx Gy Bx By Wx Wy
    result["color_rx"] = _chroma_coord(data[27], data[25], 6)
    result["color_ry"] = _chroma_coord(data[28], data[25], 4)
    result["color_gx"] = _chroma_coord(data[29], data[25], 2)
    result["color_gy"] = _chroma_coord(data[30], data[25], 0)
    result["color_bx"] = _chroma_coord(data[31], data[26], 6)
    result["color_by"] = _chroma_coord(data[32], data[26], 4)
    result["color_wx"] = _chroma_coord(data[33], data[26], 2)
    result["color_wy"] = _chroma_coord(data[34], data[26], 0)

    primaries: _Primaries = (
        (result["color_rx"], result["color_ry"]),
        (result["color_gx"], result["color_gy"]),
        (result["color_bx"], result["color_by"]),
    )
    result["gamut_srgb_pct"]     = calculate_gamut_coverage(primaries, SRGB_PRIMARIES)
    result["gamut_p3_pct"]       = calculate_gamut_coverage(primaries, DCI_P3_PRIMARIES)
    result["gamut_adobergb_pct"] = calculate_gamut_coverage(primaries, ADOBE_RGB_PRIMARIES)

    # ── Descriptor blocks (bytes 54–125, four × 18 bytes) ─────────────────
    for offset in (54, 72, 90, 108):
        desc = data[offset: offset + 18]
        if len(desc) < 18:
            continue
        # Non-pixel-clock descriptor starts with 0x00 0x00 0x00
        if desc[0] == 0 and desc[1] == 0 and desc[2] == 0:
            tag  = desc[3]
            text = desc[5:18].decode("latin-1").rstrip("\n\r \x00").rstrip()
            if tag == _DESC_MONITOR_NAME:
                result["monitor_name"] = text
            elif tag == _DESC_SERIAL_NUMBER:
                result["serial_number"] = text
            elif tag == _DESC_RANGE_LIMITS:
                # Byte 5: min vertical rate (Hz)
                # Byte 6: max vertical rate (Hz)
                result["min_refresh_hz"] = int(desc[5])
                result["max_refresh_hz"] = int(desc[6])

    # Fall back to binary serial if no ASCII serial was found
    if result["serial_number"] is None and serial_bin not in (0, 0xFFFF_FFFF):
        result["serial_number"] = str(serial_bin)

    # ── CTA-861 extension block (HDR static metadata) ─────────────────
    if len(data) >= 256 and data[128] == 0x02:  # CTA extension tag
        ext = data[128:256]
        dtd_offset = ext[2]
        pos = 4
        while pos < dtd_offset and pos < 126:
            tag = (ext[pos] >> 5) & 0x07
            length = ext[pos] & 0x1F
            if pos + 1 + length > 126:
                break
            block_data = ext[pos + 1: pos + 1 + length]
            if tag == 7 and length > 0:  # Extended tag
                ext_tag = block_data[0]
                if ext_tag == 6 and length > 1:  # HDR Static Metadata
                    eotf = block_data[1]
                    hdrs = []
                    if eotf & 0x04:
                        hdrs.append("HDR10")
                    if eotf & 0x08:
                        hdrs.append("HLG")
                    if hdrs:
                        result["hdr_supported"] = ", ".join(hdrs)
                    if length > 3:
                        code = block_data[3]
                        result["max_luminance_nits"] = int(
                            50 * (2 ** (code / 32))
                        )
            pos += 1 + length

    return result


# ---------------------------------------------------------------------------
# Gamut coverage calculation
# ---------------------------------------------------------------------------

def _polygon_area(vertices: Sequence[_Chroma]) -> float:
    """Return the area of a polygon using the shoelace formula.

    Args:
        vertices: Sequence of (x, y) vertices in order.

    Returns:
        Non-negative polygon area.
    """
    n = len(vertices)
    if n < 3:
        return 0.0
    area = 0.0
    for i in range(n):
        j = (i + 1) % n
        area += vertices[i][0] * vertices[j][1]
        area -= vertices[j][0] * vertices[i][1]
    return abs(area) * 0.5


def _edge_intersect(
    p1: _Chroma, p2: _Chroma, p3: _Chroma, p4: _Chroma
) -> Optional[_Chroma]:
    """Return the intersection point of lines p1–p2 and p3–p4, or ``None``."""
    x1, y1 = p1;  x2, y2 = p2
    x3, y3 = p3;  x4, y4 = p4
    denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4)
    if abs(denom) < 1e-12:
        return None
    t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom
    return (x1 + t * (x2 - x1), y1 + t * (y2 - y1))


def _clip_by_edge(
    polygon: List[_Chroma], e_start: _Chroma, e_end: _Chroma
) -> List[_Chroma]:
    """Clip *polygon* against a single directed edge using Sutherland-Hodgman.

    Points to the left of the directed edge (e_start → e_end) are considered
    "inside".
    """
    if not polygon:
        return []

    def _inside(pt: _Chroma) -> bool:
        ex = e_end[0] - e_start[0]
        ey = e_end[1] - e_start[1]
        return ex * (pt[1] - e_start[1]) - ey * (pt[0] - e_start[0]) >= 0

    output: List[_Chroma] = []
    for i, curr in enumerate(polygon):
        prev = polygon[i - 1]
        if _inside(curr):
            if not _inside(prev):
                pt = _edge_intersect(prev, curr, e_start, e_end)
                if pt is not None:
                    output.append(pt)
            output.append(curr)
        elif _inside(prev):
            pt = _edge_intersect(prev, curr, e_start, e_end)
            if pt is not None:
                output.append(pt)
    return output


def calculate_gamut_coverage(
    display_primaries: _Primaries, reference_primaries: _Primaries
) -> Optional[float]:
    """Return the percentage of *reference_primaries* gamut covered by *display_primaries*.

    Uses the Sutherland-Hodgman algorithm to compute the intersection area of
    two triangles in CIE xy space, then divides by the reference triangle area.

    Args:
        display_primaries:   Three (x, y) primaries of the display under test.
        reference_primaries: Three (x, y) primaries of the reference colour space.

    Returns:
        Coverage as a float in the range 0–100 (clamped), or ``None`` if the
        reference triangle has zero area.
    """
    ref_area = _polygon_area(reference_primaries)
    if ref_area < 1e-12:
        return None

    clipped: List[_Chroma] = list(display_primaries)
    ref = list(reference_primaries)
    n = len(ref)
    for i in range(n):
        clipped = _clip_by_edge(clipped, ref[i], ref[(i + 1) % n])
        if not clipped:
            return 0.0

    intersection = _polygon_area(clipped)
    return min(100.0, intersection / ref_area * 100.0)


# ---------------------------------------------------------------------------
# Platform-specific display info collection
# ---------------------------------------------------------------------------

def _run(cmd: List[str], timeout: int = 8) -> str:
    """Run *cmd* and return stdout, or empty string on any error."""
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
        )
        return result.stdout
    except Exception:
        return ""


def _get_macos_info() -> Dict[str, Any]:
    """Collect display info on macOS via ``system_profiler`` and ``ioreg``."""
    info: Dict[str, Any] = {}

    # ── system_profiler ───────────────────────────────────────────────────
    raw = _run(["system_profiler", "SPDisplaysDataType", "-json"])
    if raw:
        try:
            data = json.loads(raw)
            displays = data.get("SPDisplaysDataType", [])
            for gpu in displays:
                for disp in gpu.get("spdisplays_ndrvs", []):
                    # Only pick up the main (primary) display
                    if disp.get("spdisplays_main") != "spdisplays_yes":
                        continue

                    # Resolution  e.g. "2560 x 1600 Retina"
                    px_res = disp.get("spdisplays_pixelresolution", "")
                    parts = px_res.replace("Retina", "").split("x")
                    if len(parts) == 2:
                        try:
                            info["resolution_width"]  = int(parts[0].strip())
                            info["resolution_height"] = int(parts[1].strip().split()[0])
                        except ValueError:
                            pass

                    # Refresh rate  e.g. "1280 x 800 @ 60.00Hz"
                    res = disp.get("spdisplays_resolution", "")
                    if "@" in res and "Hz" in res:
                        try:
                            hz_part = res.split("@")[1].replace("Hz", "").strip()
                            info["refresh_rate"] = float(hz_part)
                        except (ValueError, IndexError):
                            pass

                    # Retina flag
                    info["is_retina"] = (
                        disp.get("spdisplays_retina") == "spdisplays_yes"
                        or "Retina" in px_res
                    )

                    # True Tone / adaptive colour
                    tt = disp.get("spdisplays_truetone", "")
                    if tt:
                        info["true_tone"] = "on" in tt.lower() or "yes" in tt.lower()

                    # HDR
                    hdr = disp.get("spdisplays_hdr", "")
                    if hdr:
                        info["hdr_tier"] = hdr
                    break
        except (json.JSONDecodeError, KeyError):
            pass

    # ── ICC / ColorSync profile ───────────────────────────────────────────
    # List profiles installed in the per-user ColorSync directory
    cs_dir = os.path.expanduser("~/Library/ColorSync/Profiles")
    if os.path.isdir(cs_dir):
        profiles = [f for f in os.listdir(cs_dir) if f.endswith(".icc")]
        if profiles:
            info["icc_profile_name"] = profiles[0]
            info["icc_profile_path"] = os.path.join(cs_dir, profiles[0])

    # ── EDID via ioreg ────────────────────────────────────────────────────
    ioreg_out = _run(["ioreg", "-l", "-d", "0", "-r", "-c", "IODisplayConnect"])
    if ioreg_out:
        # Look for IODisplayEDID = <hexdata>
        import re
        m = re.search(r'"IODisplayEDID"\s*=\s*<([0-9a-fA-F]+)>', ioreg_out)
        if m:
            try:
                edid_bytes = bytes.fromhex(m.group(1))
                edid_info  = parse_edid(edid_bytes)
                info.update({k: v for k, v in edid_info.items() if v is not None})
            except ValueError:
                pass

    # ── Adaptive sync (ProMotion) ─────────────────────────────────────────
    # ProMotion displays report a range in system_profiler
    if info.get("min_refresh_hz") and info.get("max_refresh_hz"):
        if info["min_refresh_hz"] != info["max_refresh_hz"]:
            info["adaptive_sync"]       = True
            info["adaptive_sync_range"] = (
                f"{info['min_refresh_hz']}–{info['max_refresh_hz']} Hz"
            )
        else:
            info["adaptive_sync"] = False

    return info


def _get_linux_info() -> Dict[str, Any]:
    """Collect display info on Linux via ``xrandr`` and ``/sys/class/drm``."""
    info: Dict[str, Any] = {}

    # ── xrandr ────────────────────────────────────────────────────────────
    xrandr_out = _run(["xrandr", "--verbose"])
    if xrandr_out:
        import re
        # Find the active (connected, with *) mode
        lines = xrandr_out.splitlines()
        for i, line in enumerate(lines):
            # "HDMI-1 connected primary 1920x1080+0+0 …"
            m = re.match(r"(\S+)\s+connected(?:\s+primary)?\s+(\d+)x(\d+)", line)
            if m:
                info["resolution_width"]  = int(m.group(2))
                info["resolution_height"] = int(m.group(3))
            # Refresh rate: look for   "   1920x1080    59.94*+"
            m2 = re.search(r"(\d+\.\d+)\*", line)
            if m2:
                info["refresh_rate"] = float(m2.group(1))

        # Adaptive sync range from "VRR" or "FreeSync" keywords
        if "vrr" in xrandr_out.lower() or "freesync" in xrandr_out.lower():
            info["adaptive_sync"] = True

    # ── Raw EDID from sysfs ───────────────────────────────────────────────
    drm_base = "/sys/class/drm"
    if os.path.isdir(drm_base):
        for connector in os.listdir(drm_base):
            edid_path = os.path.join(drm_base, connector, "edid")
            if os.path.isfile(edid_path):
                try:
                    with open(edid_path, "rb") as fh:
                        edid_bytes = fh.read()
                    if len(edid_bytes) >= 128:
                        edid_info = parse_edid(edid_bytes)
                        info.update({k: v for k, v in edid_info.items() if v is not None})
                        break
                except OSError:
                    pass

    # ── ICC / color profile ───────────────────────────────────────────────
    # colord / colormgr (if available)
    colormgr_out = _run(["colormgr", "get-profiles"])
    if colormgr_out:
        import re
        m = re.search(r"Object Path:\s+(.+)", colormgr_out)
        if m:
            info["icc_profile_path"] = m.group(1).strip()
        m2 = re.search(r"Profile ID:\s+(.+)", colormgr_out)
        if m2:
            info["icc_profile_name"] = m2.group(1).strip()

    # ── HDR (check for HDR-capable connector) ─────────────────────────────
    drm_hdr_path = "/sys/class/drm/card0-HDMI-A-1/hdr_output_metadata"
    if os.path.exists(drm_hdr_path):
        info["hdr_tier"] = "HDR (OS-signalled)"

    return info


def _get_windows_info() -> Dict[str, Any]:
    """Collect display info on Windows via PowerShell / WMI."""
    info: Dict[str, Any] = {}

    # ── Resolution via Win32_VideoController ─────────────────────────────
    ps_res = _run([
        "powershell", "-NoProfile", "-Command",
        "Get-WmiObject Win32_VideoController | "
        "Select-Object CurrentHorizontalResolution,CurrentVerticalResolution,"
        "CurrentRefreshRate,VideoModeDescription | ConvertTo-Json",
    ])
    if ps_res:
        try:
            vc = json.loads(ps_res)
            if isinstance(vc, list):
                vc = vc[0]
            info["resolution_width"]  = vc.get("CurrentHorizontalResolution")
            info["resolution_height"] = vc.get("CurrentVerticalResolution")
            rr = vc.get("CurrentRefreshRate")
            if rr:
                info["refresh_rate"] = float(rr)
        except (json.JSONDecodeError, KeyError, TypeError):
            pass

    # ── EDID from registry ────────────────────────────────────────────────
    ps_edid = _run([
        "powershell", "-NoProfile", "-Command",
        "$item = Get-ItemProperty "
        "'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\DISPLAY\\*\\*\\Device Parameters' "
        "-Name EDID -ErrorAction SilentlyContinue "
        "| Where-Object { $_.EDID } | Select-Object -First 1; "
        "if ($item) { [BitConverter]::ToString($item.EDID).Replace('-','') }",
    ])
    if ps_edid:
        try:
            edid_bytes = bytes.fromhex(ps_edid.strip())
            edid_info  = parse_edid(edid_bytes)
            info.update({k: v for k, v in edid_info.items() if v is not None})
        except ValueError:
            pass

    # ── HDR from EDID CTA extension (already parsed above) ───────────────
    # If parse_edid found HDR static metadata, promote it to hdr_tier.
    hdr_sup = info.get("hdr_supported")
    if hdr_sup:
        lum = info.get("max_luminance_nits")
        info["hdr_tier"] = hdr_sup + (f" (~{lum} cd/m²)" if lum else "")

    # ── Adaptive sync from EDID refresh range ────────────────────────────
    rr_min = info.get("min_refresh_hz")
    rr_max = info.get("max_refresh_hz")
    if rr_min and rr_max and rr_max > rr_min:
        info["adaptive_sync"] = True
        info["adaptive_sync_range"] = f"{rr_min}–{rr_max} Hz"

    # ── ICC profile ───────────────────────────────────────────────────────
    ps_icc = _run([
        "powershell", "-NoProfile", "-Command",
        "Get-ChildItem "
        "'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Class\\"
        "{4d36e96e-e325-11ce-bfc1-08002be10318}\\*' "
        "-ErrorAction SilentlyContinue | ForEach-Object { "
        "$p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue; "
        "if ($p.ICMProfile) { $p.ICMProfile -join ', ' } "
        "} | Select-Object -First 1",
    ])
    if ps_icc and ps_icc.strip():
        names = ps_icc.strip()
        info["icc_profile_name"] = names
        info["icc_profile_path"] = os.path.join(
            os.environ.get("SystemRoot", r"C:\Windows"),
            "system32", "spool", "drivers", "color",
            names.split(",")[0].strip(),
        )

    return info


# ---------------------------------------------------------------------------
# Main public collection function
# ---------------------------------------------------------------------------

def _make_base_info() -> Dict[str, Any]:
    """Return a blank display info dict with all recognised keys set to ``None``."""
    return {
        "platform":            platform.system(),
        "resolution_width":    None,
        "resolution_height":   None,
        "refresh_rate":        None,
        "adaptive_sync":       None,
        "adaptive_sync_range": None,
        "hdr_tier":            None,
        "hdr_active":          None,
        "hdr_supported":       None,
        "max_luminance_nits":  None,
        "true_tone":           None,
        "icc_profile_name":    None,
        "icc_profile_path":    None,
        "is_retina":           None,
        # EDID fields
        "manufacturer_id":     None,
        "manufacturer_name":   None,
        "product_code":        None,
        "serial_number":       None,
        "manufacture_week":    None,
        "manufacture_year":    None,
        "monitor_name":        None,
        "panel_type":          None,
        "min_refresh_hz":      None,
        "max_refresh_hz":      None,
        "color_rx": None, "color_ry": None,
        "color_gx": None, "color_gy": None,
        "color_bx": None, "color_by": None,
        "color_wx": None, "color_wy": None,
        "gamut_srgb_pct":      None,
        "gamut_p3_pct":        None,
        "gamut_adobergb_pct":  None,
    }


def get_display_info() -> Dict[str, Any]:
    """Collect display information for the primary display.

    Queries the OS and, where possible, reads raw EDID data.  All values are
    self-reported by the manufacturer or OS and have **not** been verified with
    measurement hardware.

    Returns:
        A dict with the following keys (absent or ``None`` when unavailable):

        ``platform``, ``resolution_width``, ``resolution_height``,
        ``refresh_rate``, ``adaptive_sync``, ``adaptive_sync_range``,
        ``hdr_tier``, ``hdr_active``, ``true_tone``, ``icc_profile_name``,
        ``icc_profile_path``, ``is_retina``,

        and all keys returned by :func:`parse_edid`:
        ``manufacturer_id``, ``manufacturer_name``, ``product_code``,
        ``serial_number``, ``manufacture_week``, ``manufacture_year``,
        ``monitor_name``, ``panel_type``, ``min_refresh_hz``,
        ``max_refresh_hz``, ``color_rx``, ``color_ry``, ``color_gx``,
        ``color_gy``, ``color_bx``, ``color_by``, ``color_wx``, ``color_wy``,
        ``gamut_srgb_pct``, ``gamut_p3_pct``, ``gamut_adobergb_pct``.
    """
    base = _make_base_info()

    system = platform.system()
    if system == "Darwin":
        platform_info = _get_macos_info()
    elif system == "Linux":
        platform_info = _get_linux_info()
    elif system == "Windows":
        platform_info = _get_windows_info()
    else:
        platform_info = {}

    base.update({k: v for k, v in platform_info.items() if v is not None})
    return base


# ---------------------------------------------------------------------------
# Multi-display collectors
# ---------------------------------------------------------------------------

def _get_macos_all_displays() -> List[Dict[str, Any]]:
    """Collect display info for *all* connected displays on macOS.

    Uses ``system_profiler SPDisplaysDataType`` to enumerate displays and
    ``ioreg -c IODisplayConnect`` to correlate raw EDID data by index.
    """
    import re as _re

    displays: List[Dict[str, Any]] = []

    raw = _run(["system_profiler", "SPDisplaysDataType", "-json"])
    if raw:
        try:
            data = json.loads(raw)
            for gpu in data.get("SPDisplaysDataType", []):
                for disp in gpu.get("spdisplays_ndrvs", []):
                    info: Dict[str, Any] = {}

                    px_res = disp.get("spdisplays_pixelresolution", "")
                    parts = px_res.replace("Retina", "").split("x")
                    if len(parts) == 2:
                        try:
                            info["resolution_width"]  = int(parts[0].strip())
                            info["resolution_height"] = int(parts[1].strip().split()[0])
                        except ValueError:
                            pass

                    res = disp.get("spdisplays_resolution", "")
                    if "@" in res and "Hz" in res:
                        try:
                            hz_part = res.split("@")[1].replace("Hz", "").strip()
                            info["refresh_rate"] = float(hz_part)
                        except (ValueError, IndexError):
                            pass

                    info["is_retina"] = (
                        disp.get("spdisplays_retina") == "spdisplays_yes"
                        or "Retina" in px_res
                    )

                    tt = disp.get("spdisplays_truetone", "")
                    if tt:
                        info["true_tone"] = "on" in tt.lower() or "yes" in tt.lower()

                    hdr = disp.get("spdisplays_hdr", "")
                    if hdr:
                        info["hdr_tier"] = hdr

                    displays.append(info)
        except (json.JSONDecodeError, KeyError):
            pass

    # Correlate EDID data by index from ioreg
    ioreg_out = _run(["ioreg", "-l", "-d", "0", "-r", "-c", "IODisplayConnect"])
    if ioreg_out:
        edid_hexes = _re.findall(
            r'"IODisplayEDID"\s*=\s*<([0-9a-fA-F]+)>', ioreg_out
        )
        for i, info in enumerate(displays):
            if i < len(edid_hexes):
                try:
                    edid_bytes = bytes.fromhex(edid_hexes[i])
                    edid_info  = parse_edid(edid_bytes)
                    info.update({k: v for k, v in edid_info.items() if v is not None})
                except ValueError:
                    pass

    # ICC profile (list from per-user ColorSync directory)
    cs_dir = os.path.expanduser("~/Library/ColorSync/Profiles")
    if os.path.isdir(cs_dir) and displays:
        profiles = [f for f in os.listdir(cs_dir) if f.endswith(".icc")]
        if profiles:
            displays[0]["icc_profile_name"] = profiles[0]
            displays[0]["icc_profile_path"] = os.path.join(cs_dir, profiles[0])

    # Adaptive sync per display
    for info in displays:
        if info.get("min_refresh_hz") and info.get("max_refresh_hz"):
            if info["min_refresh_hz"] != info["max_refresh_hz"]:
                info["adaptive_sync"]       = True
                info["adaptive_sync_range"] = (
                    f"{info['min_refresh_hz']}–{info['max_refresh_hz']} Hz"
                )
            else:
                info["adaptive_sync"] = False

    return displays if displays else [_get_macos_info()]


def _get_linux_all_displays() -> List[Dict[str, Any]]:
    """Collect display info for *all* connected displays on Linux.

    Uses ``xrandr`` for resolution/refresh data and ``/sys/class/drm/*/edid``
    for raw EDID.  Connector names are normalised to match xrandr output.
    """
    import re as _re

    # Parse xrandr to build a map of connected output → display info
    xrandr_out = _run(["xrandr", "--verbose"])
    xrandr_displays: Dict[str, Dict[str, Any]] = {}
    if xrandr_out:
        lines = xrandr_out.splitlines()
        current_conn: Optional[str] = None
        for line in lines:
            m = _re.match(r"^(\S+)\s+connected(?:\s+primary)?\s+(\d+)x(\d+)", line)
            if m:
                current_conn = m.group(1)
                xrandr_displays[current_conn] = {
                    "resolution_width":  int(m.group(2)),
                    "resolution_height": int(m.group(3)),
                }
            elif current_conn:
                m2 = _re.search(r"(\d+\.\d+)\*", line)
                if m2:
                    xrandr_displays[current_conn]["refresh_rate"] = float(m2.group(1))
                    current_conn = None  # one refresh rate per output is sufficient

        if "vrr" in xrandr_out.lower() or "freesync" in xrandr_out.lower():
            for di in xrandr_displays.values():
                di["adaptive_sync"] = True

    all_infos: List[Dict[str, Any]] = []

    drm_base = "/sys/class/drm"
    if os.path.isdir(drm_base):
        for connector in sorted(os.listdir(drm_base)):
            edid_path = os.path.join(drm_base, connector, "edid")
            if not os.path.isfile(edid_path):
                continue
            try:
                with open(edid_path, "rb") as fh:
                    edid_bytes = fh.read()
                if len(edid_bytes) < 128:
                    continue
                info: Dict[str, Any] = {}

                # Normalise DRM name to xrandr convention:
                # "card0-HDMI-A-1" → try "HDMI-A-1", then "HDMI-1"
                stripped = _re.sub(r"^card\d+-", "", connector)
                fallback_connector = _re.sub(r"-A-(\d+)$", r"-\1", stripped)
                xrandr_info = (
                    xrandr_displays.get(stripped)
                    or xrandr_displays.get(fallback_connector)
                )
                if xrandr_info:
                    info.update(xrandr_info)

                edid_info = parse_edid(edid_bytes)
                info.update({k: v for k, v in edid_info.items() if v is not None})
                all_infos.append(info)
            except OSError:
                pass

    # Fall back to xrandr-only entries if no EDID files were readable
    if not all_infos:
        for di in xrandr_displays.values():
            all_infos.append(dict(di))

    # ICC profile
    colormgr_out = _run(["colormgr", "get-profiles"])
    if colormgr_out and all_infos:
        m = _re.search(r"Object Path:\s+(.+)", colormgr_out)
        if m:
            all_infos[0]["icc_profile_path"] = m.group(1).strip()
        m2 = _re.search(r"Profile ID:\s+(.+)", colormgr_out)
        if m2:
            all_infos[0]["icc_profile_name"] = m2.group(1).strip()

    drm_hdr_path = "/sys/class/drm/card0-HDMI-A-1/hdr_output_metadata"
    if os.path.exists(drm_hdr_path) and all_infos:
        all_infos[0]["hdr_tier"] = "HDR (OS-signalled)"

    return all_infos if all_infos else [_get_linux_info()]


def _get_windows_all_displays() -> List[Dict[str, Any]]:
    """Collect display info for *all* connected displays on Windows.

    Uses ``Win32_VideoController`` for resolution/refresh and the EDID registry
    key for raw EDID data; EDIDs are correlated to controllers by enumeration
    order.
    """
    all_infos: List[Dict[str, Any]] = []

    ps_res = _run([
        "powershell", "-NoProfile", "-Command",
        "Get-WmiObject Win32_VideoController | "
        "Select-Object CurrentHorizontalResolution,CurrentVerticalResolution,"
        "CurrentRefreshRate,VideoModeDescription | ConvertTo-Json",
    ])
    if ps_res:
        try:
            vc_list = json.loads(ps_res)
            if isinstance(vc_list, dict):
                vc_list = [vc_list]
            for vc in vc_list:
                if not isinstance(vc, dict):
                    continue
                info: Dict[str, Any] = {}
                info["resolution_width"]  = vc.get("CurrentHorizontalResolution")
                info["resolution_height"] = vc.get("CurrentVerticalResolution")
                rr = vc.get("CurrentRefreshRate")
                if rr:
                    info["refresh_rate"] = float(rr)
                all_infos.append(info)
        except (json.JSONDecodeError, KeyError, TypeError):
            pass

    # Retrieve all available EDIDs and correlate by index
    ps_edid = _run([
        "powershell", "-NoProfile", "-Command",
        "$items = Get-ItemProperty "
        "'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\DISPLAY\\*\\*\\Device Parameters' "
        "-Name EDID -ErrorAction SilentlyContinue "
        "| Where-Object { $_.EDID }; "
        "foreach ($item in $items) { "
        "[BitConverter]::ToString($item.EDID).Replace('-',''); "
        "Write-Output '---' }",
    ])
    edid_hexes: List[str] = []
    if ps_edid:
        for chunk in ps_edid.strip().split("---"):
            chunk = chunk.strip()
            if chunk:
                edid_hexes.append(chunk)

    for i, info in enumerate(all_infos):
        if i < len(edid_hexes):
            try:
                edid_bytes = bytes.fromhex(edid_hexes[i])
                edid_info  = parse_edid(edid_bytes)
                info.update({k: v for k, v in edid_info.items() if v is not None})
            except ValueError:
                pass

    for info in all_infos:
        hdr_sup = info.get("hdr_supported")
        if hdr_sup:
            lum = info.get("max_luminance_nits")
            info["hdr_tier"] = hdr_sup + (f" (~{lum} cd/m²)" if lum else "")

        rr_min = info.get("min_refresh_hz")
        rr_max = info.get("max_refresh_hz")
        if rr_min and rr_max and rr_max > rr_min:
            info["adaptive_sync"]       = True
            info["adaptive_sync_range"] = f"{rr_min}–{rr_max} Hz"

    # ICC profile (first entry only — per-display ICC on Windows requires WCS)
    ps_icc = _run([
        "powershell", "-NoProfile", "-Command",
        "Get-ChildItem "
        "'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\Class\\"
        "{4d36e96e-e325-11ce-bfc1-08002be10318}\\*' "
        "-ErrorAction SilentlyContinue | ForEach-Object { "
        "$p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue; "
        "if ($p.ICMProfile) { $p.ICMProfile -join ', ' } "
        "} | Select-Object -First 1",
    ])
    if ps_icc and ps_icc.strip() and all_infos:
        names = ps_icc.strip()
        all_infos[0]["icc_profile_name"] = names
        all_infos[0]["icc_profile_path"] = os.path.join(
            os.environ.get("SystemRoot", r"C:\Windows"),
            "system32", "spool", "drivers", "color",
            names.split(",")[0].strip(),
        )

    return all_infos if all_infos else [_get_windows_info()]


def get_all_displays_info() -> List[Dict[str, Any]]:
    """Collect display information for *all* connected displays.

    Queries the OS for each connected monitor and returns one dict per
    display.  The primary/built-in display is always first in the list.
    All values are self-reported and have **not** been verified with
    measurement hardware.

    Returns:
        A non-empty list of dicts.  Each dict has the same keys as returned
        by :func:`get_display_info`.  Falls back to
        ``[get_display_info()]`` when multi-display enumeration fails or is
        unsupported on the current platform.
    """
    system = platform.system()
    try:
        if system == "Darwin":
            platform_displays = _get_macos_all_displays()
        elif system == "Linux":
            platform_displays = _get_linux_all_displays()
        elif system == "Windows":
            platform_displays = _get_windows_all_displays()
        else:
            platform_displays = [{}]
    except Exception:  # pragma: no cover – defensive catch for unexpected OS errors
        platform_displays = [{}]

    result: List[Dict[str, Any]] = []
    for platform_info in platform_displays:
        base = _make_base_info()
        base.update({k: v for k, v in platform_info.items() if v is not None})
        result.append(base)

    return result if result else [get_display_info()]


# ---------------------------------------------------------------------------
# Scorecard
# ---------------------------------------------------------------------------

def scorecard(info: Dict[str, Any]) -> List[Dict[str, Any]]:
    """Compare *info* against Apple display reference targets.

    Args:
        info: Dict returned by :func:`get_display_info`.

    Returns:
        List of dicts, each with keys:

        ``metric``  – human-readable metric name,
        ``value``   – measured / reported value (str),
        ``target``  – reference target (str),
        ``result``  – ``"PASS"``, ``"REVIEW"``, or ``"FAIL"``,
        ``note``    – explanatory note.
    """
    rows: List[Dict[str, Any]] = []

    # ── PPI ───────────────────────────────────────────────────────────────
    w = info.get("resolution_width")
    h = info.get("resolution_height")
    ppi_value = None
    if w and h:
        # We don't have physical diagonal here; report raw resolution only
        # A precise PPI requires the physical diagonal from the user.
        ppi_value = f"{w}×{h} px (diagonal required for PPI)"

    rows.append({
        "metric": "Resolution / PPI",
        "value":  ppi_value or "Unknown",
        "target": f"≥{APPLE_TARGETS['ppi_macbook']} PPI (MacBook) / "
                  f"≥{APPLE_TARGETS['ppi_iphone']} PPI (iPhone-class)",
        "result": "REVIEW",
        "note":   "Enter physical diagonal in the Retina Checker to compute PPI.",
    })

    # ── DCI-P3 colour gamut ───────────────────────────────────────────────
    p3 = info.get("gamut_p3_pct")
    target_p3 = APPLE_TARGETS["p3_pct_min"]
    if p3 is None:
        rows.append({
            "metric": "DCI-P3 Gamut Coverage",
            "value":  "Unknown ⚠️",
            "target": f"≥{target_p3:.0f}%",
            "result": "REVIEW",
            "note":   "EDID chromaticity data not available.",
        })
    else:
        result = "PASS" if p3 >= target_p3 else ("REVIEW" if p3 >= 90 else "FAIL")
        rows.append({
            "metric": "DCI-P3 Gamut Coverage",
            "value":  f"{p3:.1f}% ⚠️",
            "target": f"≥{target_p3:.0f}%",
            "result": result,
            "note":   "Self-reported; not independently verified.",
        })

    # ── Refresh rate ──────────────────────────────────────────────────────
    rr = info.get("refresh_rate") or info.get("max_refresh_hz")
    rr_min = info.get("min_refresh_hz", 60)
    rr_max = info.get("max_refresh_hz") or rr
    tgt_min = APPLE_TARGETS["refresh_min_hz"]
    tgt_max = APPLE_TARGETS["refresh_max_hz"]
    if rr is None:
        rows.append({
            "metric": "Refresh Rate",
            "value":  "Unknown ⚠️",
            "target": f"{tgt_min}–{tgt_max} Hz adaptive",
            "result": "REVIEW",
            "note":   "Refresh rate not available from OS.",
        })
    else:
        passes = (
            rr_min is not None and rr_max is not None
            and rr_min <= tgt_min and rr_max >= tgt_max
        )
        rows.append({
            "metric": "Refresh Rate",
            "value":  f"{rr:.0f} Hz ⚠️",
            "target": f"{tgt_min}–{tgt_max} Hz adaptive",
            "result": "PASS" if passes else "REVIEW",
            "note":   "Self-reported; not independently verified.",
        })

    # ── HDR ───────────────────────────────────────────────────────────────
    hdr = info.get("hdr_tier")
    rows.append({
        "metric": "HDR Tier",
        "value":  str(hdr) + " ⚠️" if hdr else "Not detected ⚠️",
        "target": APPLE_TARGETS["hdr_tier"],
        "result": "REVIEW",
        "note":   "HDR tier is self-reported by the display / OS.",
    })

    # ── Hardware verified placeholder ─────────────────────────────────────
    for metric, unit in [
        ("Measured ΔE (avg)", "ΔE units"),
        ("Measured Peak Brightness", "cd/m²"),
        ("Measured Contrast Ratio", ":1"),
        ("Measured Gamut vs sRGB", "%"),
    ]:
        rows.append({
            "metric": metric,
            "value":  "— (requires colorimeter)",
            "target": "Hardware verified",
            "result": "—",
            "note":   "Requires a colorimeter and DisplayCAL / CalMAN integration.",
        })

    return rows
