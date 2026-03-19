"""
Processor Information Collection
=================================
Collects CPU / SoC data from the operating system and scores it against
Apple M-series reference targets.  All values are self-reported by the OS
and should be treated as indicative, not authoritative.

Public API
----------
``get_processor_info()``   – Return a ``ProcessorInfo`` dict for the active CPU.
``scorecard(info)``        – Compare ``ProcessorInfo`` against M-series targets.
``PERFORMANCE_TIERS``      – Ordered tier definitions used for scoring.
``M_SERIES_REFERENCE``     – Apple M-series reference data used as the baseline.
"""

from __future__ import annotations

import platform
import re
import subprocess
import sys
from typing import Any, Dict, List, Optional

# ---------------------------------------------------------------------------
# Apple M-series reference data (M3 Pro as the mid-range baseline)
# ---------------------------------------------------------------------------

M_SERIES_REFERENCE: Dict[str, Any] = {
    # Chip characteristics
    "chip_name":              "Apple M3 Pro",
    "architecture":           "ARM",
    "vendor":                 "Apple",
    "performance_cores":      6,
    "efficiency_cores":       6,
    "total_cores":            12,
    "threads":                12,

    # Performance tier scores (0–100 scale, 100 = best-in-class)
    "single_core_score":      100,   # Reference baseline
    "multi_core_score":       100,
    "efficiency_score":       100,
    "sustained_score":        100,
    "igpu_score":             100,
    "real_world_score":       100,
    "platform_score":         100,

    # Thermal / power
    "tdp_watts":              30,    # M3 Pro configurable TDP
    "has_active_cooling":     False,

    # Ecosystem
    "os_native":              "macOS",
    "virtualization_notes":   "Rosetta 2 for x86 apps; Linux VMs via UTM/VMware",
}

# ---------------------------------------------------------------------------
# Performance-tier database
# ---------------------------------------------------------------------------
# Each tier describes a class of chips.  ``match_patterns`` are
# case-insensitive substrings matched against the CPU model string returned by
# the OS.  Scores are on a 0–100 scale relative to M3 Pro = 100.

PERFORMANCE_TIERS: List[Dict[str, Any]] = [
    # ── Apple Silicon ──────────────────────────────────────────────────────
    {
        "tier": "apple_m4_ultra",
        "label": "Apple M4 Ultra",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m4 ultra"],
        "single_core": 115, "multi_core": 200, "efficiency": 110,
        "sustained": 110, "igpu": 190, "real_world": 130,
        "platform": 95,
        "typical_tdp": 60, "active_cooling": False,
        "strengths": ["Extreme multi-core throughput", "Best-in-class efficiency", "Unified memory"],
        "weaknesses": ["Mac-only ecosystem", "Limited x86 app compatibility"],
    },
    {
        "tier": "apple_m4_max",
        "label": "Apple M4 Max",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m4 max"],
        "single_core": 112, "multi_core": 160, "efficiency": 108,
        "sustained": 108, "igpu": 160, "real_world": 120,
        "platform": 95,
        "typical_tdp": 45, "active_cooling": False,
        "strengths": ["Excellent multi-core", "Very efficient", "Unified memory"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m4_pro",
        "label": "Apple M4 Pro",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m4 pro"],
        "single_core": 110, "multi_core": 130, "efficiency": 106,
        "sustained": 106, "igpu": 130, "real_world": 115,
        "platform": 95,
        "typical_tdp": 30, "active_cooling": False,
        "strengths": ["Fast single-core", "Great efficiency", "Unified memory"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m4",
        "label": "Apple M4",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["apple m4"],
        "single_core": 108, "multi_core": 110, "efficiency": 104,
        "sustained": 104, "igpu": 115, "real_world": 110,
        "platform": 95,
        "typical_tdp": 15, "active_cooling": False,
        "strengths": ["Excellent efficiency", "Fast single-core"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m3_ultra",
        "label": "Apple M3 Ultra",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m3 ultra"],
        "single_core": 103, "multi_core": 185, "efficiency": 105,
        "sustained": 105, "igpu": 180, "real_world": 125,
        "platform": 95,
        "typical_tdp": 60, "active_cooling": False,
        "strengths": ["Extreme multi-core throughput", "Best-in-class efficiency"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m3_max",
        "label": "Apple M3 Max",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m3 max"],
        "single_core": 102, "multi_core": 155, "efficiency": 103,
        "sustained": 103, "igpu": 155, "real_world": 118,
        "platform": 95,
        "typical_tdp": 45, "active_cooling": False,
        "strengths": ["Excellent multi-core", "Unified memory bandwidth"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m3_pro",
        "label": "Apple M3 Pro",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m3 pro"],
        "single_core": 100, "multi_core": 100, "efficiency": 100,
        "sustained": 100, "igpu": 100, "real_world": 100,
        "platform": 95,
        "typical_tdp": 30, "active_cooling": False,
        "strengths": ["Balanced performance and efficiency", "Unified memory"],
        "weaknesses": ["Mac-only ecosystem", "Limited app virtualization"],
    },
    {
        "tier": "apple_m3",
        "label": "Apple M3",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["apple m3"],
        "single_core": 98, "multi_core": 85, "efficiency": 98,
        "sustained": 97, "igpu": 88, "real_world": 93,
        "platform": 95,
        "typical_tdp": 15, "active_cooling": False,
        "strengths": ["Great efficiency", "Fast single-core for the TDP"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m2_ultra",
        "label": "Apple M2 Ultra",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m2 ultra"],
        "single_core": 96, "multi_core": 175, "efficiency": 96,
        "sustained": 96, "igpu": 165, "real_world": 118,
        "platform": 95,
        "typical_tdp": 60, "active_cooling": False,
        "strengths": ["Massive multi-core throughput"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m2_max",
        "label": "Apple M2 Max",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m2 max"],
        "single_core": 95, "multi_core": 145, "efficiency": 95,
        "sustained": 95, "igpu": 145, "real_world": 112,
        "platform": 95,
        "typical_tdp": 40, "active_cooling": False,
        "strengths": ["Strong multi-core", "Excellent efficiency"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m2_pro",
        "label": "Apple M2 Pro",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m2 pro"],
        "single_core": 93, "multi_core": 90, "efficiency": 93,
        "sustained": 92, "igpu": 90, "real_world": 90,
        "platform": 95,
        "typical_tdp": 30, "active_cooling": False,
        "strengths": ["Good balance of performance and efficiency"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m2",
        "label": "Apple M2",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["apple m2"],
        "single_core": 90, "multi_core": 78, "efficiency": 91,
        "sustained": 90, "igpu": 82, "real_world": 86,
        "platform": 95,
        "typical_tdp": 15, "active_cooling": False,
        "strengths": ["Great efficiency for everyday tasks"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m1_ultra",
        "label": "Apple M1 Ultra",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m1 ultra"],
        "single_core": 87, "multi_core": 160, "efficiency": 88,
        "sustained": 88, "igpu": 150, "real_world": 110,
        "platform": 95,
        "typical_tdp": 60, "active_cooling": False,
        "strengths": ["Outstanding multi-core efficiency"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m1_pro",
        "label": "Apple M1 Pro",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["m1 pro"],
        "single_core": 86, "multi_core": 82, "efficiency": 87,
        "sustained": 86, "igpu": 80, "real_world": 82,
        "platform": 95,
        "typical_tdp": 30, "active_cooling": False,
        "strengths": ["Pioneer of Apple's high-efficiency pro chips"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    {
        "tier": "apple_m1",
        "label": "Apple M1",
        "vendor": "Apple", "arch": "ARM",
        "match_patterns": ["apple m1"],
        "single_core": 84, "multi_core": 72, "efficiency": 85,
        "sustained": 84, "igpu": 72, "real_world": 78,
        "platform": 95,
        "typical_tdp": 15, "active_cooling": False,
        "strengths": ["Excellent efficiency, strong single-core"],
        "weaknesses": ["Mac-only ecosystem"],
    },
    # ── Intel – Core Ultra (Meteor Lake / Arrow Lake) ──────────────────────
    {
        "tier": "intel_core_ultra_9_285",
        "label": "Intel Core Ultra 9 285",
        "vendor": "Intel", "arch": "x86_64",
        "match_patterns": ["ultra 9 285", "core ultra 9 285"],
        "single_core": 105, "multi_core": 125, "efficiency": 62,
        "sustained": 68, "igpu": 52, "real_world": 95,
        "platform": 88,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Very fast single-core", "Strong multi-core", "Full x86 app ecosystem"],
        "weaknesses": ["High TDP", "Thermal throttling under sustained load", "Weaker iGPU vs M-series"],
    },
    {
        "tier": "intel_core_ultra_7_265",
        "label": "Intel Core Ultra 7 265",
        "vendor": "Intel", "arch": "x86_64",
        "match_patterns": ["ultra 7 265", "core ultra 7 265"],
        "single_core": 102, "multi_core": 115, "efficiency": 60,
        "sustained": 65, "igpu": 50, "real_world": 92,
        "platform": 88,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Strong single-core", "Full x86 ecosystem"],
        "weaknesses": ["Power hungry", "Thermal management critical"],
    },
    {
        "tier": "intel_core_ultra_5_245",
        "label": "Intel Core Ultra 5 245",
        "vendor": "Intel", "arch": "x86_64",
        "match_patterns": ["ultra 5 245", "core ultra 5 245"],
        "single_core": 98, "multi_core": 98, "efficiency": 58,
        "sustained": 62, "igpu": 48, "real_world": 88,
        "platform": 88,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Competitive single-core", "x86 compatibility"],
        "weaknesses": ["Efficiency gap vs M-series"],
    },
    {
        "tier": "intel_core_ultra_h",
        "label": "Intel Core Ultra (H-series, 14th-gen)",
        "vendor": "Intel", "arch": "x86_64",
        "match_patterns": [
            "ultra 9 185h", "ultra 7 165h", "ultra 7 155h",
            "ultra 5 125h", "ultra 5 135h",
        ],
        "single_core": 97, "multi_core": 108, "efficiency": 55,
        "sustained": 60, "igpu": 45, "real_world": 88,
        "platform": 88,
        "typical_tdp": 45, "active_cooling": True,
        "strengths": ["Good multi-core burst", "x86 app ecosystem", "NPU for AI tasks"],
        "weaknesses": ["Significant efficiency gap vs M-series", "Throttles under sustained load"],
    },
    {
        "tier": "intel_core_i9_13",
        "label": "Intel Core i9 13th-gen",
        "vendor": "Intel", "arch": "x86_64",
        "match_patterns": ["i9-13", "i9 13"],
        "single_core": 99, "multi_core": 118, "efficiency": 45,
        "sustained": 55, "igpu": 35, "real_world": 90,
        "platform": 88,
        "typical_tdp": 125, "active_cooling": True,
        "strengths": ["Fast burst performance", "Widest app compatibility"],
        "weaknesses": ["Very high TDP", "Poor efficiency", "Weak integrated graphics"],
    },
    {
        "tier": "intel_core_i7_13",
        "label": "Intel Core i7 13th-gen",
        "vendor": "Intel", "arch": "x86_64",
        "match_patterns": ["i7-13", "i7 13"],
        "single_core": 95, "multi_core": 105, "efficiency": 48,
        "sustained": 55, "igpu": 33, "real_world": 86,
        "platform": 88,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Strong burst, wide ecosystem"],
        "weaknesses": ["Efficiency lags behind M-series significantly"],
    },
    {
        "tier": "intel_core_i5_13",
        "label": "Intel Core i5 13th-gen",
        "vendor": "Intel", "arch": "x86_64",
        "match_patterns": ["i5-13", "i5 13"],
        "single_core": 90, "multi_core": 90, "efficiency": 50,
        "sustained": 52, "igpu": 32, "real_world": 80,
        "platform": 88,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Solid everyday performance", "x86 compatibility"],
        "weaknesses": ["Weaker multi-core vs M-series Pro", "Efficiency gap"],
    },
    # ── AMD Ryzen ──────────────────────────────────────────────────────────
    {
        "tier": "amd_ryzen_9_9000",
        "label": "AMD Ryzen 9 9000-series (Zen 5)",
        "vendor": "AMD", "arch": "x86_64",
        "match_patterns": ["ryzen 9 9", "ryzen 9 9900", "ryzen 9 9950", "ryzen 9 9700"],
        "single_core": 108, "multi_core": 130, "efficiency": 68,
        "sustained": 72, "igpu": 55, "real_world": 98,
        "platform": 85,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Top-tier single-core", "Best x86 multi-core", "Improved efficiency"],
        "weaknesses": ["Still trails M-series on efficiency per watt", "Discrete GPU needed for graphics"],
    },
    {
        "tier": "amd_ryzen_9_7000",
        "label": "AMD Ryzen 9 7000-series (Zen 4)",
        "vendor": "AMD", "arch": "x86_64",
        "match_patterns": ["ryzen 9 7", "ryzen 9 7900", "ryzen 9 7950", "ryzen 9 7700"],
        "single_core": 103, "multi_core": 122, "efficiency": 62,
        "sustained": 68, "igpu": 48, "real_world": 94,
        "platform": 85,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Competitive single-core", "Strong multi-core", "x86 ecosystem"],
        "weaknesses": ["Power-hungry under load", "Weak integrated graphics"],
    },
    {
        "tier": "amd_ryzen_7_9700x",
        "label": "AMD Ryzen 7 9700X (Zen 5)",
        "vendor": "AMD", "arch": "x86_64",
        "match_patterns": ["ryzen 7 9700", "ryzen 7 9800"],
        "single_core": 106, "multi_core": 112, "efficiency": 70,
        "sustained": 72, "igpu": 50, "real_world": 95,
        "platform": 85,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Excellent single-core speed", "Good efficiency for x86"],
        "weaknesses": ["Discrete GPU required for serious graphics"],
    },
    {
        "tier": "amd_ryzen_7_7700x",
        "label": "AMD Ryzen 7 7000-series (Zen 4)",
        "vendor": "AMD", "arch": "x86_64",
        "match_patterns": ["ryzen 7 7700", "ryzen 7 7800"],
        "single_core": 100, "multi_core": 105, "efficiency": 60,
        "sustained": 65, "igpu": 45, "real_world": 89,
        "platform": 85,
        "typical_tdp": 65, "active_cooling": True,
        "strengths": ["Strong everyday performance", "x86 compatibility"],
        "weaknesses": ["Efficiency gap vs M-series"],
    },
    {
        "tier": "amd_ryzen_ai_hx",
        "label": "AMD Ryzen AI HX (Strix Point / Hawk Point)",
        "vendor": "AMD", "arch": "x86_64",
        "match_patterns": [
            "ryzen ai 9 hx", "ryzen ai 7 hx", "ryzen ai 5 hx",
            "ryzen ai 9 365", "ryzen ai 9 370", "ryzen ai 7 350",
        ],
        "single_core": 100, "multi_core": 110, "efficiency": 72,
        "sustained": 70, "igpu": 78, "real_world": 92,
        "platform": 85,
        "typical_tdp": 28, "active_cooling": True,
        "strengths": ["Best x86 integrated graphics", "Good NPU for AI workloads", "Improved efficiency"],
        "weaknesses": ["Sustained performance still lags M-series Pro"],
    },
    {
        "tier": "amd_ryzen_hx",
        "label": "AMD Ryzen HX (laptop, Zen 4/5)",
        "vendor": "AMD", "arch": "x86_64",
        "match_patterns": [
            "ryzen 9 hx", "ryzen 7 hx", "hx 370", "hx 3d",
            "ryzen 9 7945hx", "ryzen 9 7940hx",
        ],
        "single_core": 99, "multi_core": 115, "efficiency": 58,
        "sustained": 62, "igpu": 42, "real_world": 90,
        "platform": 85,
        "typical_tdp": 55, "active_cooling": True,
        "strengths": ["Strong laptop multi-core burst"],
        "weaknesses": ["Thermal throttling under prolonged load", "Efficiency gap"],
    },
    # ── Qualcomm Snapdragon X ──────────────────────────────────────────────
    {
        "tier": "qualcomm_x_elite",
        "label": "Qualcomm Snapdragon X Elite",
        "vendor": "Qualcomm", "arch": "ARM",
        "match_patterns": ["snapdragon x elite", "x1e", "x elite"],
        "single_core": 95, "multi_core": 100, "efficiency": 88,
        "sustained": 85, "igpu": 75, "real_world": 82,
        "platform": 70,
        "typical_tdp": 23, "active_cooling": True,
        "strengths": ["Very efficient", "Good integrated GPU", "Strong NPU"],
        "weaknesses": ["x86 app emulation overhead", "Ecosystem maturity still growing"],
    },
    {
        "tier": "qualcomm_x_plus",
        "label": "Qualcomm Snapdragon X Plus",
        "vendor": "Qualcomm", "arch": "ARM",
        "match_patterns": ["snapdragon x plus", "x1p", "x plus"],
        "single_core": 92, "multi_core": 85, "efficiency": 86,
        "sustained": 82, "igpu": 68, "real_world": 78,
        "platform": 70,
        "typical_tdp": 23, "active_cooling": True,
        "strengths": ["Efficient ARM performance", "Good battery life"],
        "weaknesses": ["x86 app compatibility gaps", "Smaller ecosystem than x86"],
    },
]

# Fallback tier for unrecognised CPUs
_UNKNOWN_TIER: Dict[str, Any] = {
    "tier": "unknown",
    "label": "Unknown / Unrecognised CPU",
    "vendor": "Unknown", "arch": "Unknown",
    "match_patterns": [],
    "single_core": None, "multi_core": None, "efficiency": None,
    "sustained": None, "igpu": None, "real_world": None,
    "platform": None,
    "typical_tdp": None, "active_cooling": None,
    "strengths": [], "weaknesses": [],
}

# ---------------------------------------------------------------------------
# Helper: match CPU model string to a tier
# ---------------------------------------------------------------------------

def _match_tier(model: str) -> Dict[str, Any]:
    """Return the best-matching performance tier for *model*.

    Matching is case-insensitive substring search.  The first tier whose
    ``match_patterns`` list contains a substring of *model* is returned.
    """
    lower = model.lower()
    for tier in PERFORMANCE_TIERS:
        for pattern in tier["match_patterns"]:
            if pattern.lower() in lower:
                return tier
    return _UNKNOWN_TIER


# ---------------------------------------------------------------------------
# Platform-specific collection
# ---------------------------------------------------------------------------

def _run(cmd: List[str], timeout: int = 5) -> str:
    """Run *cmd* and return stdout as text; return '' on any error."""
    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
        )
        return result.stdout.strip()
    except Exception:
        return ""


def _get_macos_info() -> Dict[str, Any]:
    """Collect processor info on macOS via sysctl and system_profiler."""
    info: Dict[str, Any] = {}

    # sysctl brand string  ────────────────────────────────────────────────
    brand = _run(["sysctl", "-n", "machdep.cpu.brand_string"])
    if brand:
        info["cpu_model"] = brand
    else:
        # Apple Silicon – machdep.cpu.brand_string is absent; read chip name
        # from system_profiler instead.
        sp_chip = _run(["system_profiler", "SPHardwareDataType"])
        m = re.search(r"Chip:\s+(.+)", sp_chip)
        if m:
            info["cpu_model"] = m.group(1).strip()

    # Core counts
    p_cores = _run(["sysctl", "-n", "hw.perflevel0.logicalcpu"])
    e_cores = _run(["sysctl", "-n", "hw.perflevel1.logicalcpu"])
    total   = _run(["sysctl", "-n", "hw.logicalcpu"])
    if p_cores.isdigit():
        info["performance_cores"] = int(p_cores)
    if e_cores.isdigit():
        info["efficiency_cores"] = int(e_cores)
    if total.isdigit():
        info["total_threads"] = int(total)

    # CPU frequency
    freq_hz = _run(["sysctl", "-n", "hw.cpufrequency_max"])
    if freq_hz.isdigit():
        info["max_freq_mhz"] = int(freq_hz) // 1_000_000

    # system_profiler for architecture / chip
    sp_out = _run(["system_profiler", "SPHardwareDataType"])
    m = re.search(r"Chip:\s+(.+)", sp_out)
    if m:
        info.setdefault("cpu_model", m.group(1).strip())
    m = re.search(r"Total Number of Cores:\s+(\d+)", sp_out)
    if m:
        info.setdefault("total_cores", int(m.group(1)))

    # Memory bandwidth (Apple Silicon only, from hw.memsize proxy)
    mem_bytes = _run(["sysctl", "-n", "hw.memsize"])
    if mem_bytes.isdigit():
        info["memory_bytes"] = int(mem_bytes)

    return info


def _get_linux_info() -> Dict[str, Any]:
    """Collect processor info on Linux via /proc/cpuinfo and lscpu."""
    info: Dict[str, Any] = {}

    # /proc/cpuinfo  ────────────────────────────────────────────────────
    try:
        with open("/proc/cpuinfo", "r", encoding="utf-8", errors="replace") as fh:
            cpuinfo = fh.read()
    except OSError:
        cpuinfo = ""

    m = re.search(r"model name\s*:\s*(.+)", cpuinfo)
    if m:
        info["cpu_model"] = m.group(1).strip()

    m = re.search(r"cpu MHz\s*:\s*([\d.]+)", cpuinfo)
    if m:
        info["current_freq_mhz"] = float(m.group(1))

    m = re.search(r"cache size\s*:\s*(.+)", cpuinfo)
    if m:
        info["l3_cache"] = m.group(1).strip()

    # Count logical CPUs (unique processor entries)
    info["total_threads"] = cpuinfo.count("processor\t:")

    # lscpu  ──────────────────────────────────────────────────────────
    lscpu = _run(["lscpu"])
    m = re.search(r"^Core\(s\) per socket:\s*(\d+)", lscpu, re.MULTILINE)
    if m:
        info["cores_per_socket"] = int(m.group(1))
    m = re.search(r"^Socket\(s\):\s*(\d+)", lscpu, re.MULTILINE)
    if m:
        sockets = int(m.group(1))
        cpk = info.get("cores_per_socket", 1)
        info["total_cores"] = sockets * cpk
    m = re.search(r"^CPU max MHz:\s*([\d.]+)", lscpu, re.MULTILINE)
    if m:
        info["max_freq_mhz"] = float(m.group(1))
    m = re.search(r"^Vendor ID:\s*(.+)", lscpu, re.MULTILINE)
    if m:
        info["vendor_id"] = m.group(1).strip()
    m = re.search(r"^Architecture:\s*(.+)", lscpu, re.MULTILINE)
    if m:
        info["architecture"] = m.group(1).strip()
    m = re.search(r"^Model name:\s*(.+)", lscpu, re.MULTILINE)
    if m:
        info.setdefault("cpu_model", m.group(1).strip())

    # On-chip performance / efficiency core split (Intel Hybrid only)
    p_core_path = "/sys/devices/cpu_core/cpus"
    e_core_path = "/sys/devices/cpu_atom/cpus"
    try:
        with open(p_core_path) as fh:
            p_range = fh.read().strip()
        info["performance_cores_range"] = p_range
    except OSError:
        pass
    try:
        with open(e_core_path) as fh:
            e_range = fh.read().strip()
        info["efficiency_cores_range"] = e_range
    except OSError:
        pass

    return info


def _get_windows_info() -> Dict[str, Any]:
    """Collect processor info on Windows via PowerShell / WMI."""
    info: Dict[str, Any] = {}

    # Win32_Processor via Get-CimInstance
    ps_script = (
        "Get-CimInstance Win32_Processor | "
        "Select-Object Name,NumberOfCores,NumberOfLogicalProcessors,"
        "MaxClockSpeed,Manufacturer,Caption | "
        "Format-List"
    )
    out = _run(["powershell", "-NoProfile", "-Command", ps_script])

    m = re.search(r"Name\s*:\s*(.+)", out)
    if m:
        info["cpu_model"] = m.group(1).strip()
    m = re.search(r"NumberOfCores\s*:\s*(\d+)", out)
    if m:
        info["total_cores"] = int(m.group(1))
    m = re.search(r"NumberOfLogicalProcessors\s*:\s*(\d+)", out)
    if m:
        info["total_threads"] = int(m.group(1))
    m = re.search(r"MaxClockSpeed\s*:\s*(\d+)", out)
    if m:
        info["max_freq_mhz"] = int(m.group(1))
    m = re.search(r"Manufacturer\s*:\s*(.+)", out)
    if m:
        info["vendor_id"] = m.group(1).strip()

    return info


# ---------------------------------------------------------------------------
# Vendor / architecture helpers
# ---------------------------------------------------------------------------

def _infer_vendor(model: str, vendor_id: str = "") -> str:
    """Return a human-friendly vendor name from the CPU model string and vendor ID."""
    combined = (model + " " + vendor_id).lower()
    if "apple" in combined or " m1" in combined or " m2" in combined or " m3" in combined or " m4" in combined:
        return "Apple"
    if "qualcomm" in combined or "snapdragon" in combined:
        return "Qualcomm"
    if "mediatek" in combined:
        return "MediaTek"
    if "amd" in combined or "ryzen" in combined or "epyc" in combined:
        return "AMD"
    if "intel" in combined or "core i" in combined or "core ultra" in combined or "xeon" in combined:
        return "Intel"
    return "Unknown"


def _infer_arch(model: str, vendor_id: str = "") -> str:
    """Return 'ARM' or 'x86_64' from model/vendor strings."""
    combined = (model + " " + vendor_id).lower()
    if any(kw in combined for kw in ("apple", "snapdragon", "arm", "aarch64", "m1", "m2", "m3", "m4")):
        return "ARM"
    # Also check the running OS architecture as a fallback
    machine = platform.machine().lower()
    if machine in ("arm64", "aarch64"):
        return "ARM"
    return "x86_64"


# ---------------------------------------------------------------------------
# Main public API
# ---------------------------------------------------------------------------

def get_processor_info() -> Dict[str, Any]:
    """Detect platform and return a ``ProcessorInfo`` dict.

    All keys are always present; values are ``None`` when unavailable.

    Keys
    ----
    platform          str   – 'Darwin', 'Linux', or 'Windows'
    cpu_model         str | None
    vendor            str | None   – 'Intel', 'AMD', 'Apple', 'Qualcomm', …
    architecture      str | None   – 'x86_64' or 'ARM'
    total_cores       int | None
    total_threads     int | None
    performance_cores int | None
    efficiency_cores  int | None
    max_freq_mhz      float | None
    current_freq_mhz  float | None
    l3_cache          str | None
    tier              Dict | None  – Matching entry from PERFORMANCE_TIERS
    """
    base: Dict[str, Any] = {
        "platform":          platform.system(),
        "cpu_model":         None,
        "vendor":            None,
        "architecture":      None,
        "total_cores":       None,
        "total_threads":     None,
        "performance_cores": None,
        "efficiency_cores":  None,
        "max_freq_mhz":      None,
        "current_freq_mhz":  None,
        "l3_cache":          None,
        "tier":              None,
    }

    system = platform.system()
    try:
        if system == "Darwin":
            platform_data = _get_macos_info()
        elif system == "Linux":
            platform_data = _get_linux_info()
        elif system == "Windows":
            platform_data = _get_windows_info()
        else:
            platform_data = {}
    except Exception:
        platform_data = {}

    base.update({k: v for k, v in platform_data.items() if v is not None})

    # Infer vendor and architecture from model string if not already set
    model = base.get("cpu_model") or ""
    vendor_id = platform_data.get("vendor_id", "")
    if model and base["vendor"] is None:
        base["vendor"] = _infer_vendor(model, vendor_id)
    if model and base["architecture"] is None:
        base["architecture"] = _infer_arch(model, vendor_id)

    # Match to a performance tier
    if model:
        base["tier"] = _match_tier(model)

    return base


# ---------------------------------------------------------------------------
# Scorecard
# ---------------------------------------------------------------------------

# Score thresholds for PASS / REVIEW / FAIL
_PASS_THRESHOLD   = 90   # ≥90% of M-series reference
_REVIEW_THRESHOLD = 70   # ≥70% of M-series reference


def _score_result(score: Optional[int]) -> str:
    """Return 'PASS', 'REVIEW', or 'FAIL' for a normalised 0–100 score."""
    if score is None:
        return "REVIEW"
    if score >= _PASS_THRESHOLD:
        return "PASS"
    if score >= _REVIEW_THRESHOLD:
        return "REVIEW"
    return "FAIL"


def _relative_score(score: Optional[int], reference: int) -> Optional[int]:
    """Return *score* as a percentage of *reference*, capped at 120."""
    if score is None:
        return None
    if reference == 0:
        return None
    return round(min(score / reference * 100, 120))


def _consumer_note(dimension: str, score: Optional[int], tier: Dict[str, Any]) -> str:
    """Return a consumer-friendly one-liner for a scoring dimension."""
    if score is None:
        return "No data available for this processor."
    pct = _relative_score(score, 100)  # reference = 100
    if pct is None:
        return "No data available."
    arch = tier.get("arch", "")
    if dimension == "single_core":
        if pct >= 105:
            return "Faster than M3 Pro for snappy app launches and UI responsiveness."
        if pct >= 90:
            return "Comparable to M3 Pro for everyday tasks and app launches."
        return "Noticeably slower than M3 Pro for single-threaded workloads."
    if dimension == "multi_core":
        if pct >= 120:
            return "Crushes M3 Pro on heavy parallel workloads (builds, exports)."
        if pct >= 100:
            return "Matches or beats M3 Pro for multi-threaded tasks."
        if pct >= 80:
            return "Reasonably competitive with M3 Pro for most multi-threaded work."
        return "Falls behind M3 Pro on heavy multi-threaded workloads."
    if dimension == "efficiency":
        if pct >= 95:
            return "Best-in-class battery life – rivals or exceeds M3 Pro efficiency."
        if pct >= 70:
            return "Decent battery life but noticeably less efficient than M3 Pro."
        return "Expect significantly shorter battery life compared to M3 Pro."
    if dimension == "sustained":
        if pct >= 95:
            return "Maintains peak performance under prolonged load – no throttling."
        if pct >= 70:
            return "Mild throttling under sustained load; still usable for most tasks."
        return "Significant throttling under sustained load – performance drops over time."
    if dimension == "igpu":
        if pct >= 95:
            return "Excellent integrated GPU – handles light gaming and media work well."
        if pct >= 70:
            return "Adequate integrated GPU for everyday graphics tasks."
        return "Weaker integrated GPU; a discrete GPU is recommended for media/graphics."
    if dimension == "real_world":
        if pct >= 95:
            return "Outstanding real-world performance across web, Office, and coding."
        if pct >= 80:
            return "Good real-world performance for productivity and content creation."
        return "Below M3 Pro in real-world productivity benchmarks."
    if dimension == "platform":
        if arch == "ARM" and "apple" not in tier.get("vendor", "").lower():
            return "ARM Windows chip: growing ecosystem but some x86 apps may need emulation."
        if arch == "ARM":
            return "macOS ecosystem: excellent for Apple workflows; some x86 app gaps."
        return "Full x86 Windows ecosystem: broadest app and game compatibility."
    return ""


def scorecard(info: Dict[str, Any]) -> List[Dict[str, Any]]:
    """Compare *info* against M-series reference targets.

    Returns a list of dicts, one per evaluation dimension.  Each dict has:
    - ``metric``   – Human-readable dimension name
    - ``value``    – Score as a fraction of M-series reference (or 'Unknown')
    - ``target``   – M-series reference score description
    - ``result``   – 'PASS', 'REVIEW', or 'FAIL'
    - ``note``     – Consumer-friendly explanation
    - ``badge``    – Short consumer label (e.g. 'Best for battery life')
    """
    rows: List[Dict[str, Any]] = []
    tier = info.get("tier") or _UNKNOWN_TIER
    ref  = M_SERIES_REFERENCE

    dimensions = [
        ("single_core",  "Single-Core Performance",          "App launches, UI responsiveness"),
        ("multi_core",   "Multi-Core Performance",           "Builds, exports, multitasking"),
        ("efficiency",   "Performance per Watt (Efficiency)", "Battery life, thermal behaviour"),
        ("sustained",    "Sustained Performance",            "Prolonged-load vs burst"),
        ("igpu",         "Integrated GPU / Accelerators",    "Media, AI/NPU, light graphics"),
        ("real_world",   "Real-World Workloads",             "Web, Office, coding, content creation"),
        ("platform",     "Platform & Ecosystem",             "App compatibility, dev workflows"),
    ]

    for key, metric, category in dimensions:
        score   = tier.get(key)         # raw score for this chip (0–100 scale)
        ref_val = ref.get(f"{key}_score", 100)  # M3 Pro reference = 100

        if score is None:
            value_str = "Unknown"
            result    = "REVIEW"
        else:
            pct       = _relative_score(score, ref_val)
            value_str = f"{pct}% of M3 Pro"
            result    = _score_result(pct)

        badge = _make_badge(key, score, tier)
        note  = _consumer_note(key, score, tier)

        rows.append({
            "metric":   metric,
            "category": category,
            "value":    value_str,
            "target":   "M3 Pro = 100%",
            "result":   result,
            "note":     note,
            "badge":    badge,
        })

    # ── Strengths & weaknesses summary ────────────────────────────────────
    strengths  = tier.get("strengths", [])
    weaknesses = tier.get("weaknesses", [])
    rows.append({
        "metric":   "Strengths",
        "category": "Summary",
        "value":    "; ".join(strengths) if strengths else "Unknown",
        "target":   "—",
        "result":   "—",
        "note":     "Key advantages of this processor.",
        "badge":    "",
    })
    rows.append({
        "metric":   "Weaknesses",
        "category": "Summary",
        "value":    "; ".join(weaknesses) if weaknesses else "Unknown",
        "target":   "—",
        "result":   "—",
        "note":     "Key limitations compared to M-series.",
        "badge":    "",
    })

    return rows


def _make_badge(dimension: str, score: Optional[int], tier: Dict[str, Any]) -> str:
    """Return a short consumer badge label for a dimension score."""
    if score is None:
        return ""
    if dimension == "efficiency" and score >= 88:
        return "Best for battery life"
    if dimension == "efficiency" and score >= 70:
        return "Good battery life"
    if dimension == "multi_core" and score >= 120:
        return "Best for heavy multitasking"
    if dimension == "multi_core" and score >= 100:
        return "Great for multitasking"
    if dimension == "single_core" and score >= 105:
        return "Fastest for everyday tasks"
    if dimension == "igpu" and score >= 75:
        return "Good for light gaming"
    if dimension == "platform" and tier.get("arch") == "x86_64":
        return "Widest x86 app compatibility"
    if dimension == "platform" and tier.get("vendor") == "Apple":
        return "Best for Apple ecosystem"
    return ""
