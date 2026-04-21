"""
Memory and Storage Information Collection
==========================================
Collects RAM and storage data from the operating system and scores it against
Apple M3 Pro reference targets.

Public API
----------
``get_memory_storage_info()``  – Return a dict of detected memory/storage data.
``scorecard(info)``            – Compare against M3 Pro targets and return rows.
``M3_PRO_REFERENCE``           – Apple M3 Pro reference values used as baseline.
"""

from __future__ import annotations

import platform
import re
import subprocess
import sys
from typing import Any, Dict, List, Optional, Tuple

# ---------------------------------------------------------------------------
# Apple M3 Pro reference values
# ---------------------------------------------------------------------------

M3_PRO_REFERENCE: Dict[str, Any] = {
    # Memory
    "ram_gb":           18.0,        # 18 GB unified memory (base config)
    "ram_type":         "LPDDR5",    # unified LPDDR5
    "ram_bandwidth_gbps": 150,       # ~150 GB/s bandwidth
    "ram_expandable":   False,       # soldered; cannot be upgraded

    # Storage
    "storage_gb":       512,         # 512 GB base SSD
    "storage_type":     "NVMe",      # custom Apple NVMe
    "storage_read_gbps": 7.4,        # ~7.4 GB/s sequential read
    "storage_expandable": False,     # soldered; cannot be upgraded
}

# ---------------------------------------------------------------------------
# Colors
# ---------------------------------------------------------------------------

_GREEN  = "#007a00"
_ORANGE = "#b85c00"
_RED    = "#cc0000"
_GRAY   = "#555555"


def _result_color(result: str) -> str:
    if result == "PASS":
        return _GREEN
    if result == "REVIEW":
        return _ORANGE
    if result == "FAIL":
        return _RED
    return _GRAY


# ---------------------------------------------------------------------------
# Platform detection helpers
# ---------------------------------------------------------------------------

def _run(cmd: List[str], timeout: int = 5) -> str:
    """Run a command and return stdout, or empty string on failure."""
    try:
        result = subprocess.run(
            cmd, capture_output=True, text=True, timeout=timeout,
        )
        return result.stdout.strip()
    except Exception:
        return ""


def _get_windows_info() -> Dict[str, Any]:
    info: Dict[str, Any] = {}

    # ── RAM ──────────────────────────────────────────────────────────────────
    mem_out = _run([
        "powershell", "-NoProfile", "-Command",
        "Get-CimInstance Win32_PhysicalMemory | "
        "Select-Object Capacity,Speed,SMBIOSMemoryType | Format-List",
    ])

    total_bytes = 0
    slots_used = 0
    for m in re.finditer(r"Capacity\s*:\s*(\d+)", mem_out):
        total_bytes += int(m.group(1))
        slots_used += 1

    speed_m = re.search(r"Speed\s*:\s*(\d+)", mem_out)
    if speed_m:
        info["ram_speed_mhz"] = int(speed_m.group(1))

    type_m = re.search(r"SMBIOSMemoryType\s*:\s*(\d+)", mem_out)
    if type_m:
        smbios = int(type_m.group(1))
        info["ram_type"] = {34: "DDR5", 26: "DDR4", 24: "DDR3", 20: "DDR3"}.get(smbios)

    # Fallback: total RAM from Win32_ComputerSystem
    if not total_bytes:
        cs_out = _run([
            "powershell", "-NoProfile", "-Command",
            "Get-CimInstance Win32_ComputerSystem | "
            "Select-Object TotalPhysicalMemory | Format-List",
        ])
        m2 = re.search(r"TotalPhysicalMemory\s*:\s*(\d+)", cs_out)
        if m2:
            total_bytes = int(m2.group(1))

    if total_bytes:
        info["total_ram_bytes"] = total_bytes

    # Slot count
    slots_out = _run([
        "powershell", "-NoProfile", "-Command",
        "(Get-CimInstance Win32_PhysicalMemoryArray).MemoryDevices",
    ])
    try:
        info["ram_slots"] = int(slots_out.strip())
    except ValueError:
        pass
    if slots_used:
        info["ram_slots_used"] = slots_used

    # ── Storage ───────────────────────────────────────────────────────────────
    disk_out = _run([
        "powershell", "-NoProfile", "-Command",
        "Get-PhysicalDisk | "
        "Select-Object FriendlyName,Size,BusType,MediaType | Format-List",
    ])

    primary_size = 0
    total_storage = 0
    primary_type: Optional[str] = None
    primary_model: Optional[str] = None

    for block in re.split(r"\r?\n\r?\n", disk_out):
        size_m = re.search(r"Size\s*:\s*(\d+)", block)
        if not size_m:
            continue
        size = int(size_m.group(1))
        total_storage += size

        bus_m   = re.search(r"BusType\s*:\s*(\w+)", block)
        media_m = re.search(r"MediaType\s*:\s*(.+)", block)
        name_m  = re.search(r"FriendlyName\s*:\s*(.+)", block)

        bus   = (bus_m.group(1)   if bus_m   else "").strip()
        media = (media_m.group(1) if media_m else "").strip()
        name  = (name_m.group(1)  if name_m  else "").strip()

        disk_type = _classify_windows_disk(bus, media)

        if size > primary_size:
            primary_size  = size
            primary_type  = disk_type
            primary_model = name

    if primary_size:
        info["primary_storage_bytes"] = primary_size
    if total_storage:
        info["total_storage_bytes"] = total_storage
    if primary_type:
        info["storage_type"] = primary_type
    if primary_model:
        info["storage_model"] = primary_model

    return info


def _classify_windows_disk(bus: str, media: str) -> str:
    if bus.lower() == "nvme":
        return "NVMe SSD"
    if bus.lower() == "sata":
        return "SATA SSD" if "ssd" in media.lower() else "HDD"
    if "ssd" in media.lower():
        return "SSD"
    if "hdd" in media.lower():
        return "HDD"
    return "Unknown"


def _get_macos_info() -> Dict[str, Any]:
    info: Dict[str, Any] = {}

    # ── RAM ──────────────────────────────────────────────────────────────────
    mem_bytes = _run(["sysctl", "-n", "hw.memsize"])
    try:
        info["total_ram_bytes"] = int(mem_bytes)
    except ValueError:
        pass

    sp_mem = _run(["system_profiler", "SPMemoryDataType"])
    if sp_mem and "No memory slots" not in sp_mem:
        type_m  = re.search(r"Type:\s+(.+)",          sp_mem)
        speed_m = re.search(r"Speed:\s+(\d+)\s*MHz",  sp_mem)
        if type_m:
            info["ram_type"] = type_m.group(1).strip()
        if speed_m:
            info["ram_speed_mhz"] = int(speed_m.group(1))
        slots_used = len(re.findall(r"Size:\s+\d+", sp_mem))
        if slots_used:
            info["ram_slots_used"] = slots_used
    else:
        info["ram_type"] = "Unified Memory (LPDDR5)"

    # ── Storage ───────────────────────────────────────────────────────────────
    sp_stor = _run(["system_profiler", "SPStorageDataType"])

    primary_size = 0
    total_storage = 0
    primary_type: Optional[str] = None
    primary_model: Optional[str] = None

    cap_matches   = re.findall(r"Capacity:\s*([\d,]+)\s*bytes", sp_stor)
    proto_matches = re.findall(r"Protocol:\s*(.+)",              sp_stor)
    medium_matches = re.findall(r"Medium Type:\s*(.+)",          sp_stor)
    name_matches  = re.findall(r"Drive:\s*(.+)",                 sp_stor)

    for i, cap_str in enumerate(cap_matches):
        try:
            cap = int(cap_str.replace(",", ""))
        except ValueError:
            continue
        total_storage += cap

        protocol = proto_matches[i].strip()  if i < len(proto_matches)  else ""
        medium   = medium_matches[i].strip() if i < len(medium_matches) else ""
        drive    = name_matches[i].strip()   if i < len(name_matches)   else ""

        disk_type = _classify_mac_disk(protocol, medium)

        if cap > primary_size:
            primary_size  = cap
            primary_type  = disk_type
            primary_model = drive

    # Fallback: diskutil
    if not total_storage:
        du = _run(["diskutil", "list"])
        m = re.search(r"(\d+\.?\d*)\s*(GB|TB)", du)
        if m:
            sz   = float(m.group(1))
            mult = 1e12 if m.group(2) == "TB" else 1e9
            total_storage = int(sz * mult)
            primary_size  = total_storage
            primary_type  = "NVMe SSD"

    if primary_size:
        info["primary_storage_bytes"] = primary_size
    if total_storage:
        info["total_storage_bytes"] = total_storage
    if primary_type:
        info["storage_type"] = primary_type
    if primary_model:
        info["storage_model"] = primary_model

    return info


def _classify_mac_disk(protocol: str, medium: str) -> str:
    if "nvme" in protocol.lower():
        return "NVMe SSD"
    if "sata" in protocol.lower():
        return "SATA SSD" if "solid" in medium.lower() else "HDD"
    if "solid" in medium.lower():
        return "SSD"
    return "NVMe SSD"   # Modern Macs default


def _get_linux_info() -> Dict[str, Any]:
    info: Dict[str, Any] = {}

    # ── RAM ──────────────────────────────────────────────────────────────────
    try:
        with open("/proc/meminfo") as f:
            meminfo = f.read()
        m = re.search(r"MemTotal:\s+(\d+)\s+kB", meminfo)
        if m:
            info["total_ram_bytes"] = int(m.group(1)) * 1024
    except OSError:
        pass

    dmi = _run(["dmidecode", "--type", "17"])
    if dmi:
        type_m  = re.search(r"Type:\s+(\w+)",          dmi)
        speed_m = re.search(r"Speed:\s+(\d+)\s*MT",    dmi)
        if type_m:
            info["ram_type"] = type_m.group(1).strip()
        if speed_m:
            info["ram_speed_mhz"] = int(speed_m.group(1))
        info["ram_slots_used"] = len(re.findall(r"Size:\s+\d+ MB", dmi))
        info["ram_slots"]      = len(re.findall(r"Memory Device",  dmi))

    # ── Storage ───────────────────────────────────────────────────────────────
    lsblk = _run(["lsblk", "-bdo", "NAME,SIZE,TYPE,ROTA,TRAN"])
    primary_size = 0
    total_storage = 0
    primary_type: Optional[str] = None

    for line in lsblk.splitlines()[1:]:
        parts = line.split()
        if len(parts) < 3 or parts[2] != "disk":
            continue
        try:
            size = int(parts[1])
        except ValueError:
            continue
        rotational = len(parts) > 3 and parts[3] == "1"
        tran       = parts[4].upper() if len(parts) > 4 else ""

        disk_type = "NVMe SSD" if tran == "NVME" else ("HDD" if rotational else "SATA SSD")
        total_storage += size
        if size > primary_size:
            primary_size = size
            primary_type = disk_type

    if primary_size:
        info["primary_storage_bytes"] = primary_size
    if total_storage:
        info["total_storage_bytes"] = total_storage
    if primary_type:
        info["storage_type"] = primary_type

    return info


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------

def get_memory_storage_info() -> Dict[str, Any]:
    """Return memory and storage information for the current system."""
    info: Dict[str, Any] = {"platform": platform.system()}
    try:
        if sys.platform == "win32":
            info.update(_get_windows_info())
        elif sys.platform == "darwin":
            info.update(_get_macos_info())
        elif sys.platform.startswith("linux"):
            info.update(_get_linux_info())
    except Exception:
        pass
    return info


# ---------------------------------------------------------------------------
# Scoring helpers
# ---------------------------------------------------------------------------

def _ram_capacity_result(ram_gb: float) -> str:
    if ram_gb >= 32:
        return "PASS"
    if ram_gb >= 16:
        return "REVIEW"
    return "FAIL"


def _ram_speed_result(ram_type: Optional[str], speed_mhz: Optional[int]) -> str:
    t = (ram_type or "").upper()
    if "DDR5" in t or "LPDDR5" in t:
        return "PASS"
    if "DDR4" in t or "LPDDR4" in t:
        return "REVIEW"
    if "DDR3" in t or "LPDDR3" in t:
        return "FAIL"
    if speed_mhz is not None:
        if speed_mhz >= 4800:
            return "PASS"
        if speed_mhz >= 2133:
            return "REVIEW"
        return "FAIL"
    return "REVIEW"


def _storage_capacity_result(storage_gb: float) -> str:
    if storage_gb >= 512:
        return "PASS"
    if storage_gb >= 256:
        return "REVIEW"
    return "FAIL"


def _storage_type_result(storage_type: Optional[str]) -> str:
    t = (storage_type or "").upper()
    if "NVME" in t:
        return "PASS"
    if "SSD" in t or "SATA" in t:
        return "REVIEW"
    return "FAIL"


def _storage_speed_result(storage_type: Optional[str]) -> Tuple[str, float]:
    t = (storage_type or "").upper()
    if "GEN5" in t or "GEN 5" in t:
        return "PASS", 12.0
    if "NVME" in t:
        return "REVIEW", 6.0   # assume Gen4 for generic NVMe
    if "SSD" in t or "SATA" in t:
        return "FAIL", 0.55
    return "FAIL", 0.15


def scorecard(info: Dict[str, Any]) -> List[Dict[str, Any]]:
    """Build and return the memory/storage scorecard rows."""
    rows: List[Dict[str, Any]] = []

    def _gb(key: str) -> Optional[float]:
        v = info.get(key)
        return v / 1_073_741_824 if v else None

    ram_gb      = _gb("total_ram_bytes")
    primary_gb  = _gb("primary_storage_bytes") or _gb("total_storage_bytes")
    ram_type    = info.get("ram_type")
    speed_mhz   = info.get("ram_speed_mhz")
    stor_type   = info.get("storage_type")

    def fmt_stor(gb: Optional[float]) -> str:
        if gb is None:
            return "Unknown"
        if gb >= 1024:
            return f"{gb / 1024:.1f} TB"
        return f"{gb:.0f} GB"

    # ── 1. RAM Capacity ───────────────────────────────────────────────────────
    if ram_gb is not None:
        result = _ram_capacity_result(ram_gb)
        value  = f"{ram_gb:.1f} GB"
    else:
        result = "REVIEW"
        value  = "Unknown"
    note = {
        "PASS":   "Meets or exceeds the M3 Pro; comfortable for demanding workloads",
        "REVIEW": "Similar to the M3 Pro base config; sufficient for most tasks",
        "FAIL":   "Below the M3 Pro 18 GB baseline; may limit heavy multitasking",
    }[result]
    rows.append({
        "metric": "RAM Capacity",
        "value":  value,
        "target": f"≥ 18 GB (M3 Pro base: {M3_PRO_REFERENCE['ram_gb']:.0f} GB)",
        "result": result,
        "note":   note,
        "color":  _result_color(result),
    })

    # ── 2. RAM Speed / Type ───────────────────────────────────────────────────
    result = _ram_speed_result(ram_type, speed_mhz)
    speed_str = f" @ {speed_mhz} MHz" if speed_mhz else ""
    value  = f"{ram_type}{speed_str}" if ram_type else (f"{speed_mhz} MHz" if speed_mhz else "Unknown")
    note = {
        "PASS":   "Modern high-bandwidth RAM; comparable generation to Apple's LPDDR5",
        "REVIEW": "Capable but older generation than Apple's LPDDR5 unified memory",
        "FAIL":   "Legacy RAM; significantly slower than Apple's unified memory",
    }[result]
    rows.append({
        "metric": "RAM Speed / Type",
        "value":  value,
        "target": "DDR5 / LPDDR5 (M3 Pro unified memory)",
        "result": result,
        "note":   note,
        "color":  _result_color(result),
    })

    # ── 3. Storage Capacity ───────────────────────────────────────────────────
    if primary_gb is not None:
        result = _storage_capacity_result(primary_gb)
        value  = fmt_stor(primary_gb)
    else:
        result = "REVIEW"
        value  = "Unknown"
    note = {
        "PASS":   "Meets or exceeds the M3 Pro base storage; room for large projects",
        "REVIEW": "Slightly below M3 Pro base; adequate for everyday use",
        "FAIL":   "Limited storage; consider an external drive for large files",
    }[result]
    rows.append({
        "metric": "Storage Capacity",
        "value":  value,
        "target": f"≥ 512 GB (M3 Pro base: {M3_PRO_REFERENCE['storage_gb']:.0f} GB)",
        "result": result,
        "note":   note,
        "color":  _result_color(result),
    })

    # ── 4. Storage Type ───────────────────────────────────────────────────────
    result = _storage_type_result(stor_type)
    value  = stor_type or "Unknown"
    note = {
        "PASS":   "NVMe SSD; matches the class of Apple's custom storage",
        "REVIEW": "SATA SSD; capable but noticeably slower than Apple's NVMe",
        "FAIL":   "Spinning disk or unknown; significantly slower than Apple SSD",
    }[result]
    rows.append({
        "metric": "Storage Type",
        "value":  value,
        "target": f"NVMe SSD (M3 Pro: {M3_PRO_REFERENCE['storage_type']})",
        "result": result,
        "note":   note,
        "color":  _result_color(result),
    })

    # ── 5. Estimated Storage Read Speed ──────────────────────────────────────
    speed_result, est_gbps = _storage_speed_result(stor_type)
    note = {
        "PASS":   "Gen 5 NVMe; faster than Apple's M3 Pro SSD",
        "REVIEW": "Gen 4 NVMe; slightly below Apple's SSD speed in sustained reads",
        "FAIL":   "Well below Apple M3 Pro SSD; large-file transfers will be slower",
    }[speed_result]
    rows.append({
        "metric": "Est. Storage Read Speed",
        "value":  f"~{est_gbps:.1f} GB/s (est.)",
        "target": f"~{M3_PRO_REFERENCE['storage_read_gbps']} GB/s (M3 Pro)",
        "result": speed_result,
        "note":   note,
        "color":  _result_color(speed_result),
    })

    # ── 6. Upgrade Potential (info row) ───────────────────────────────────────
    slots      = info.get("ram_slots")
    slots_used = info.get("ram_slots_used")
    if slots is not None and slots_used is not None:
        free = slots - slots_used
        upgrade_note = (
            f"RAM: {free} slot(s) free — can upgrade. Storage: additional drives possible."
            if free > 0 else
            "RAM: all slots occupied but replaceable. Storage: additional drives possible."
        )
    else:
        upgrade_note = (
            "PC RAM and storage are typically user-upgradeable; "
            "Apple Silicon memory and storage are soldered and cannot be upgraded."
        )
    rows.append({
        "metric": "PC Upgrade Advantage",
        "value":  "Upgradeable",
        "target": "Fixed (Apple soldered)",
        "result": "—",
        "note":   upgrade_note,
        "color":  _GRAY,
    })

    return rows
