# pc-eval
Tools to evaluate the hardware of a PC.

---

## Display Evaluator  *(implements [issue #2](https://github.com/usivagna/pc-eval/issues/2))*

A tabbed desktop GUI (`display_eval.py`) that collects and presents comprehensive
display data in two clearly labelled categories: **Self-Reported / Unverified**
(from the OS and EDID) and placeholders for **Hardware Verified** results
(which require a colorimeter).

### Features

#### Tab 1 – Self-Reported Info
All values are displayed with a prominent ⚠️ warning that they are manufacturer-declared
and have not been independently verified.

- Detected resolution and refresh rate
- Adaptive sync support and range
- HDR support tier (as signalled to the OS)
- Colour gamut coverage (sRGB, DCI-P3, Adobe RGB percentages calculated from
  EDID chromaticity coordinates using the Sutherland-Hodgman algorithm)
- CIE xy chromaticity coordinates parsed directly from EDID
- Active ICC / colour profile name and path
- True Tone / ambient colour adjustment status (macOS)
- Panel interface type from EDID (DisplayPort, HDMI, etc.)
- Manufacturer, model name, serial number, and manufacture date from EDID

#### Tab 2 – Visual Evaluation
Generate and view fullscreen test patterns; record a 1–5 rating and free-text
notes for each:

| Pattern | What to observe |
|---------|-----------------|
| Solid White | Uniformity, bright-spot clouding, backlight bleed |
| 50% Gray | Mid-tone uniformity and panel unevenness |
| Solid Black | Backlight bleed, IPS glow, VA blackout zones |
| Black→White Gradient | Banding and abrupt tonal transitions |
| Gamma Ramp Patches | Evenly-spaced brightness steps |
| UFO Scrolling Bars | Ghosting and smearing at three scroll speeds |
| Colour Checker Grid | Hue accuracy across primaries, secondaries, skin tones, neutrals |

#### Tab 3 – Refresh & Sync
- Reports refresh rate, adaptive sync support and range from the OS / EDID.
- **Frame-Pacing Test** – animated fullscreen bar with live frame counter and
  measured FPS readout so the user can visually confirm smoothness.

#### Tab 4 – HDR & Colour
- Active HDR mode from OS APIs.
- Currently active ICC profile and colour space.
- True Tone / ambient adaptation status (macOS).

#### Tab 5 – Scorecard
Compares self-reported specs against Apple display reference targets:

| Metric | Target |
|--------|--------|
| PPI | ≥ 218 PPI (MacBook) / ≥ 254 PPI (iPhone-class) |
| DCI-P3 gamut | ≥ 95% |
| HDR | DisplayHDR 1000 |
| Refresh | 1–120 Hz adaptive |

Each metric is labelled **PASS**, **REVIEW**, or **FAIL** with an explanation
that self-reported values are unverified.  Four **Hardware Verified** placeholder
rows (ΔE, peak brightness, contrast ratio, measured gamut) are stubbed out with
a note that they require a colorimeter and DisplayCAL / CalMAN integration.

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

All 60 tests cover EDID parsing, gamut coverage calculation, polygon geometry,
scorecard logic, and the `get_display_info()` interface.

---

## Retina Display Checker

A Python/tkinter desktop GUI that tells you whether your display qualifies as a
**Retina Display** (as defined by Apple) based on your screen's resolution,
physical size, and your assumed viewing distance.

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
