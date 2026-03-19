# pc-eval
Tools to evaluate the hardware of a PC.

---

## Project structure

```
pc-eval/
├── display_info.py          # Core library: EDID parsing, gamut maths, OS queries
├── display_eval.py          # Display Evaluator GUI (tkinter)
├── retina_checker.py        # Retina Display Checker GUI (tkinter)
├── test_display_info.py     # Unit tests for display_info.py (67 tests)
└── test_retina_checker.py   # Unit tests for retina_checker.py (23 tests)
```

**Requirements:** Python 3.8+, tkinter (bundled on macOS/Windows; `python3-tk`
on Linux).  No third-party packages are required.

---

## Display Evaluator

A desktop GUI (`display_eval.py`) that auto-detects your display's specs,
highlights key values, and scores them against Apple's Retina Display reference
targets.

### Multi-monitor support

When more than one monitor is connected the evaluator automatically enumerates
all displays and shows an **independent scorecard tab for each one**.  The
primary display is always the first tab.

### Detected display information

The following values are read from the OS and EDID and labelled as
self-reported / unverified:

- Resolution and refresh rate
- Adaptive sync support and range
- HDR support tier (as signalled to the OS)
- Colour gamut coverage — sRGB, DCI-P3, and Adobe RGB percentages calculated
  from EDID chromaticity coordinates using the Sutherland-Hodgman polygon
  clipping algorithm
- Active ICC / colour profile name
- True Tone / ambient colour adjustment status (macOS)
- Panel interface type from EDID (DisplayPort, HDMI, etc.)
- Manufacturer, model name, serial number, and manufacture date from EDID

### Scorecard

Compares self-reported specs against Apple display reference targets:

| Metric | Target | Result labels |
|--------|--------|--------------|
| Pixel Density (PPI) | ≥ min PPI for selected viewing distance | PASS / FAIL |
| DCI-P3 gamut | ≥ 95% | PASS / REVIEW / FAIL |
| Refresh Rate | ≥ 120 Hz | PASS / REVIEW / FAIL |
| HDR Support | HDR detected | PASS / FAIL |

An overall letter grade (A–F) is calculated from the four metrics.  Four
**Hardware Verified** placeholder rows (ΔE, peak brightness, contrast ratio,
measured gamut) are stubbed out with a note that they require a colorimeter
and DisplayCAL / CalMAN integration.

### Platform support

| Platform | Resolution / Refresh | EDID | ICC Profile | HDR / Sync |
|----------|---------------------|------|-------------|------------|
| macOS | `system_profiler` | `ioreg` | `~/Library/ColorSync/Profiles` | `system_profiler` |
| Linux | `xrandr` | `/sys/class/drm/*/edid` | `colormgr` | `xrandr` |
| Windows | WMI / PowerShell | Registry | WMI | WMI |

### Running the Display Evaluator

```bash
python3 display_eval.py
```

### Running the Display Evaluator tests

```bash
python3 -m unittest test_display_info -v
```

All 67 tests cover EDID parsing, gamut coverage calculation, polygon geometry,
scorecard logic, `get_display_info()`, and the new `get_all_displays_info()`
multi-monitor API.

---

## Retina Display Checker

A Python/tkinter desktop GUI (`retina_checker.py`) that tells you whether your
display qualifies as a **Retina Display** (as defined by Apple) based on your
screen's resolution, physical size, and your assumed viewing distance.

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

