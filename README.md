# pc-eval
Tools to evaluate the hardware of a PC.

---

## .NET MAUI App (Primary — WinUI 3 on Windows)

The application has been migrated to **.NET MAUI** (Multi-platform App UI), which
uses **WinUI 3** as its native UI layer on Windows and runs natively on macOS,
iOS, and Android as well.

### Project structure

```
src/
├── PCEval.sln                        # Visual Studio solution
├── PCEval/                           # .NET MAUI application
│   ├── PCEval.csproj                 # Multi-target project (net9.0-windows/macos/ios/android)
│   ├── MauiProgram.cs                # App entry point and DI setup
│   ├── App.xaml / App.xaml.cs        # Application class
│   ├── AppShell.xaml / .cs           # Tab-bar shell
│   ├── Models/
│   │   ├── DisplayInfo.cs            # Display data model
│   │   ├── DisplayScorecardRow.cs    # Scorecard row model
│   │   ├── MemoryStorageInfo.cs      # Memory & storage data model
│   │   ├── MemoryStorageScorecardRow.cs  # Memory & storage scorecard row
│   │   ├── PerformanceTier.cs        # CPU tier data model
│   │   ├── ProcessorInfo.cs          # Processor data model
│   │   └── ProcessorScorecardRow.cs  # Processor scorecard row
│   ├── Services/
│   │   ├── DisplayLogic.cs           # EDID parsing, gamut maths, scorecard (port of display_info.py)
│   │   ├── DisplayService.cs         # OS-level display detection (Windows/macOS/Linux)
│   │   ├── IDisplayService.cs        # Display service interface
│   │   ├── IMemoryStorageService.cs  # Memory & storage service interface
│   │   ├── IProcessorService.cs      # Processor service interface
│   │   ├── MemoryStorageLogic.cs     # Memory & storage scorecard vs M3 Pro baseline
│   │   ├── MemoryStorageService.cs   # OS-level RAM/storage detection (Windows/macOS/Linux)
│   │   ├── ProcessorLogic.cs         # Tier database and scorecard (port of processor_info.py)
│   │   └── ProcessorService.cs       # OS-level CPU detection (Windows/macOS/Linux)
│   ├── ViewModels/
│   │   ├── DisplayViewModel.cs       # Display tab ViewModel (MVVM)
│   │   ├── MemoryStorageViewModel.cs # Memory & Storage tab ViewModel (MVVM)
│   │   ├── ProcessorViewModel.cs     # Processor tab ViewModel (MVVM)
│   │   └── RetinaCheckerViewModel.cs # Retina Checker ViewModel
│   ├── Views/
│   │   ├── DisplayPage.xaml / .cs        # Display evaluation tab
│   │   ├── MemoryStoragePage.xaml / .cs  # Memory & Storage evaluation tab
│   │   ├── ProcessorPage.xaml / .cs      # Processor evaluation tab
│   │   └── RetinaCheckerPage.xaml / .cs  # Retina Display Checker tab
│   └── Platforms/
│       ├── Windows/                  # WinUI 3 entry point + package manifest
│       ├── MacCatalyst/              # macOS entry point
│       ├── iOS/                      # iOS entry point
│       └── Android/                  # Android entry point
└── PCEval.Tests/                     # xUnit test project
    ├── DisplayLogicTests.cs              # Tests for EDID parsing, gamut, scorecard
    ├── MemoryStorageLogicTests.cs        # Tests for memory & storage scoring
    ├── ProcessorLogicTests.cs            # Tests for tier matching and scoring
    └── RetinaCheckerTests.cs             # Tests for PPI and Retina calculations
```

### Download & Install (pre-built releases)

Pre-built packages for every tagged release are published automatically on the
[GitHub Releases page](https://github.com/usivagna/pc-eval/releases).

| Platform | File | Notes |
|----------|------|-------|
| **Windows** (x64) | `PCEval-<version>-windows-x64.zip` | Extract and run `PCEval.exe` |
| **macOS** (Mac Catalyst) | `PCEval-<version>-macos.pkg` or `PCEval-<version>-macos.zip` | `.pkg` when available (double-click to install); otherwise `.zip` containing a `.app` bundle — drag to Applications. Allow the unsigned app in **System Settings → Privacy & Security**. |
| **Android** | `PCEval-<version>-android.apk` | Enable **Install unknown apps** for your browser/file manager before sideloading |

> **iOS** builds are not included in automated releases because they require an
> Apple Developer certificate. Build from source (see below) or use TestFlight.

### Requirements (build from source)

| Requirement | Version |
|-------------|---------|
| .NET SDK    | 9.0 or later |
| .NET MAUI workload | `dotnet workload install maui` |
| Windows target | Windows 10 1903 / SDK 10.0.19041+ |
| macOS target | Xcode 15+ (macOS 14+) |

### Install the MAUI workload (build from source)

```bash
dotnet workload install maui
```

### Build and run on Windows (WinUI 3)

```bash
cd src
dotnet build PCEval/PCEval.csproj -f net9.0-windows10.0.19041.0
dotnet run   --project PCEval/PCEval.csproj -f net9.0-windows10.0.19041.0
```

### Build and run on macOS

```bash
cd src
dotnet build PCEval/PCEval.csproj -f net9.0-maccatalyst
dotnet run   --project PCEval/PCEval.csproj -f net9.0-maccatalyst
```

### Run the unit tests (no MAUI workload needed)

```bash
cd src
dotnet test PCEval.Tests/PCEval.Tests.csproj
```

All 74 tests cover EDID parsing, gamut coverage (Sutherland-Hodgman), scorecard
logic, tier matching, and Retina PPI calculations.

### Features

**Tab 1 – Display**

Auto-detects all connected monitors and scores each one independently against
Apple Retina Display reference targets.  Multi-monitor support: a dropdown
selector appears when more than one monitor is connected.

- Resolution, refresh rate, adaptive sync range
- HDR support tier (from EDID CTA-861 HDR Static Metadata)
- Colour gamut coverage — sRGB, DCI-P3, and Adobe RGB (Sutherland-Hodgman)
- Physical diagonal size (from EDID)
- ICC / colour profile name
- Panel interface type (from EDID)

Scorecard per display: Pixel Density (PPI), DCI-P3 Gamut, Refresh Rate, HDR Support.

**Tab 2 – Processor**

Auto-detects the CPU / SoC and scores it across seven dimensions (single-core,
multi-core, efficiency, sustained, GPU, real-world, platform) vs the Apple M3 Pro
baseline.  Includes strengths, weaknesses, and consumer-friendly notes.

**Tab 3 – Retina Checker**

Standalone Retina Display Checker — enter your screen diagonal and viewing
distance to see whether your display qualifies as a Retina Display.

### Platform support

| Platform | Resolution / Refresh | EDID | ICC Profile | HDR / Sync |
|----------|---------------------|------|-------------|------------|
| Windows (WinUI 3) | WMI Win32_VideoController | Registry (PnP) | WMI registry | EDID CTA-861 |
| macOS | `system_profiler` | `ioreg` | ColorSync | `system_profiler` |
| Linux | `xrandr` | `/sys/class/drm/*/edid` | Not currently detected | `xrandr` |

---

## Legacy Python Tools

The original Python/tkinter tools remain in the repository root for reference.



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
| Linux | `xrandr` | `/sys/class/drm/*/edid` | Not currently detected | `xrandr` |
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

