# pc-eval
Tools to evaluate the hardware of a PC.

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
