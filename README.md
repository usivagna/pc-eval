# pc-eval
Tools to evaluate the hardware of a PC.

---

## Project structure

```
pc-eval/
├── display_info.py          # Core library: EDID parsing, gamut maths, OS queries
├── display_eval.py          # PC Evaluator GUI — Display + Processor tabs (tkinter)
├── processor_info.py        # Core library: CPU/SoC detection and scorecard
├── retina_checker.py        # Retina Display Checker GUI (tkinter)
├── test_display_info.py     # Unit tests for display_info.py (67 tests)
├── test_processor_info.py   # Unit tests for processor_info.py
└── test_retina_checker.py   # Unit tests for retina_checker.py (23 tests)
```

**Requirements:** Python 3.8+, tkinter (bundled on macOS/Windows; `python3-tk`
on Linux).  No third-party packages are required.

---

## PC Evaluator (`display_eval.py`)

A two-tab desktop GUI that scores your hardware against Apple standards.

```bash
python3 display_eval.py
```

### Tab 1 – Display

Auto-detects all connected monitors and scores each one independently against
Apple Retina Display reference targets.

**Multi-monitor support:** when more than one monitor is connected a dropdown
selector appears in the Display tab allowing you to switch between displays.
Each display's detected specs and scorecard update independently.

**Detected per display (self-reported from OS / EDID):**

- Resolution, refresh rate, and adaptive sync range
- HDR support tier
- Colour gamut coverage — sRGB, DCI-P3, and Adobe RGB percentages from EDID
  chromaticity coordinates (Sutherland-Hodgman polygon clipping algorithm)
- Physical diagonal size (from EDID, auto-filled in the input field)
- ICC / colour profile name
- True Tone / ambient colour adjustment (macOS)
- Panel interface type, manufacturer, model, serial, and manufacture date

**Scorecard per display:**

| Metric | Target | Result |
|--------|--------|--------|
| Pixel Density (PPI) | ≥ min PPI for selected viewing distance | PASS / FAIL |
| DCI-P3 gamut | ≥ 95% | PASS / REVIEW / FAIL |
| Refresh Rate | ≥ 120 Hz | PASS / REVIEW / FAIL |
| HDR Support | HDR detected | PASS / FAIL |

An overall letter grade (A–F) is calculated from the four metrics.

### Tab 2 – Processor

Auto-detects the CPU / SoC and scores it across seven dimensions (single-core,
multi-core, memory bandwidth, GPU, ML performance, power efficiency, and core
count) vs the Apple M3 Pro baseline.

### Platform support

| Platform | Resolution / Refresh | EDID | ICC Profile | HDR / Sync |
|----------|---------------------|------|-------------|------------|
| macOS | `system_profiler` | `ioreg` | `~/Library/ColorSync/Profiles` | `system_profiler` |
| Linux | `xrandr` | `/sys/class/drm/*/edid` | `colormgr` | `xrandr` |
| Windows | WMI / PowerShell | `Get-PnpDevice` (Registry) | WMI | WMI |

### Running the tests

```bash
python3 -m unittest test_display_info -v
```

All 67 tests cover EDID parsing, gamut coverage calculation, polygon geometry,
scorecard logic, `get_display_info()`, and the `get_all_displays_info()`
multi-monitor API.

---

## Retina Display Checker (`retina_checker.py`)

A standalone Python/tkinter desktop GUI that tells you whether your display
qualifies as a **Retina Display** (as defined by Apple) based on your screen's
resolution, physical size, and your assumed viewing distance.

### How it works

Apple defines a Retina Display as one where the pixel density is high enough
that individual pixels cannot be distinguished by the human eye at a typical
viewing distance.  The threshold is derived from the eye's angular resolution
limit of **one arcminute**:

```
min_ppi = 1 / (distance_inches × tan(π / 10800))
        ≈ 3438 / distance_inches
```

The app computes your display's actual PPI from its resolution and physical
diagonal size, then compares it against the threshold for the selected viewing
distance.

### Requirements

| Requirement | Version |
|-------------|---------|
| Python | 3.8 or later |
| tkinter | bundled with Python on most platforms |

> **Linux users:** tkinter is sometimes shipped separately.  Install it with:
> ```bash
> # Debian / Ubuntu
> sudo apt-get install python3-tk
>
> # Fedora / RHEL
> sudo dnf install python3-tkinter
>
> # Arch
> sudo pacman -S tk
> ```
>
> **macOS / Windows:** tkinter is included with the standard Python installer
> from [python.org](https://www.python.org/downloads/) — no extra steps needed.

No third-party packages are required.

### Installation

```bash
# 1. Clone the repository
git clone https://github.com/usivagna/pc-eval.git
cd pc-eval

# 2. (Optional) create and activate a virtual environment
python3 -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
```

### Running the app

```bash
python3 retina_checker.py
```

The window will open automatically and detect your screen's resolution.

#### Usage

1. **Screen diagonal** – type the physical diagonal size of your monitor in
   inches (e.g. `27` for a 27-inch monitor).  The value is printed on the box
   or can be found in your display's spec sheet.
2. **Viewing distance** – choose your typical distance from the screen using
   the dropdown menu.
3. The result updates instantly and shows:
   - Your display's computed **PPI**
   - The **minimum PPI** required for Retina at the selected distance
   - A green ✓ **Retina Display** or red ✗ **Not a Retina Display** verdict
   - A plain-English explanation of the result

### Running the tests

```bash
python3 -m unittest test_retina_checker -v
```

All 23 tests should pass.

