"""
Display Evaluator
=================
A tabbed desktop GUI for comprehensive display evaluation.

Tabs
----
1. Self-Reported Info  – OS/EDID data with ⚠️ unverified warnings.
2. Visual Evaluation   – Fullscreen test patterns + 1–5 rating per pattern.
3. Refresh & Sync      – Live frame-pacing test and adaptive sync info.
4. HDR & Colour        – HDR mode and ICC colour-pipeline status.
5. Scorecard           – PASS / REVIEW / FAIL comparison vs Apple targets.

Run with:
    python3 display_eval.py
"""

from __future__ import annotations

import math
import time
import tkinter as tk
from tkinter import font, ttk
from typing import Any, Dict, List, Optional, Tuple

import display_info as di

# ---------------------------------------------------------------------------
# Colour constants
# ---------------------------------------------------------------------------
_GREEN  = "#007a00"
_ORANGE = "#b85c00"
_RED    = "#cc0000"
_GRAY   = "#555555"
_WARN   = "#aa6600"

# Result → colour mapping
_RESULT_COLOR = {
    "PASS":   _GREEN,
    "REVIEW": _ORANGE,
    "FAIL":   _RED,
    "—":      _GRAY,
}

# Test patterns: (id, label, description)
_PATTERNS: List[Tuple[str, str, str]] = [
    ("white",    "Solid White",
     "Check for uniformity, bright-spot clouding, and backlight bleed."),
    ("gray50",   "50% Gray",
     "Check for uniformity and panel unevenness at mid-tone."),
    ("black",    "Solid Black",
     "Check for backlight bleed, IPS glow, or VA blackout zones."),
    ("gradient", "Black→White Gradient",
     "Look for banding or abrupt tonal transitions."),
    ("gamma",    "Gamma Ramp Patches",
     "Compare brightness steps; each patch should appear evenly spaced."),
    ("scroll",   "UFO Scrolling Bars",
     "Observe ghosting or smearing behind moving bars."),
    ("checker",  "Colour Checker Grid",
     "Judge hue accuracy and colour rendering across primaries, "
     "secondaries, skin tones, and neutrals."),
]


# ===========================================================================
# Test-pattern window
# ===========================================================================

class PatternWindow(tk.Toplevel):
    """A borderless fullscreen window displaying a single test pattern."""

    # Colour checker grid: (R, G, B, label)
    _CHECKER_COLORS: List[Tuple[int, int, int, str]] = [
        # Row 1 – primaries
        (220,  20,  60, "Red"),
        ( 34, 139,  34, "Green"),
        ( 30, 100, 220, "Blue"),
        (255, 210,   0, "Yellow"),
        (200,   0, 200, "Magenta"),
        (  0, 200, 200, "Cyan"),
        # Row 2 – secondaries / skin tones
        (255, 120,  60, "Orange"),
        (120,  60,  20, "Brown"),
        (255, 200, 180, "Skin 1"),
        (220, 160, 120, "Skin 2"),
        (180, 110,  70, "Skin 3"),
        (130,  70,  30, "Skin 4"),
        # Row 3 – neutrals
        (255, 255, 255, "White"),
        (200, 200, 200, "L.Gray"),
        (160, 160, 160, "M.Gray"),
        (100, 100, 100, "D.Gray"),
        ( 50,  50,  50, "V.Dark"),
        (  0,   0,   0, "Black"),
        # Row 4 – extra hues
        (255, 255, 128, "Lt Yellow"),
        (128, 255, 128, "Lt Green"),
        (128, 200, 255, "Lt Blue"),
        (255, 128, 255, "Lt Magenta"),
        (128, 240, 240, "Lt Cyan"),
        (255, 180, 100, "Peach"),
        # Row 5 – deep hues
        (128,   0,   0, "Dk Red"),
        (  0, 100,   0, "Dk Green"),
        (  0,   0, 128, "Dk Blue"),
        (128, 128,   0, "Olive"),
        (128,   0, 128, "Purple"),
        (  0, 128, 128, "Teal"),
    ]

    def __init__(self, master: tk.Misc, pattern_id: str) -> None:
        super().__init__(master)
        self._pattern_id = pattern_id
        self._scroll_x   = 0
        self._after_id: Optional[str] = None

        self.attributes("-fullscreen", True)
        self.configure(bg="black")
        self.bind("<Escape>", lambda _e: self._close())
        self.bind("<Button-1>", lambda _e: self._close())

        self._canvas = tk.Canvas(self, bg="black", highlightthickness=0)
        self._canvas.pack(fill="both", expand=True)

        self.after(50, self._draw)

    # ------------------------------------------------------------------

    def _close(self) -> None:
        if self._after_id:
            self.after_cancel(self._after_id)
        self.destroy()

    def _draw(self) -> None:
        """Draw the pattern selected by ``_pattern_id``."""
        c = self._canvas
        c.delete("all")
        W = self.winfo_screenwidth()
        H = self.winfo_screenheight()

        dispatch = {
            "white":    lambda: self._fill(c, W, H, "#ffffff"),
            "gray50":   lambda: self._fill(c, W, H, "#808080"),
            "black":    lambda: self._fill(c, W, H, "#000000"),
            "gradient": lambda: self._gradient(c, W, H),
            "gamma":    lambda: self._gamma_patches(c, W, H),
            "scroll":   lambda: self._scrolling_bars(c, W, H),
            "checker":  lambda: self._colour_checker(c, W, H),
        }
        dispatch.get(self._pattern_id, lambda: self._fill(c, W, H, "#404040"))()

        # Dismiss hint
        c.create_text(
            W // 2, H - 30,
            text="Press Esc or click to close",
            fill="#ffffff", font=("Helvetica", 12),
        )

    # -- individual patterns ----------------------------------------------

    @staticmethod
    def _fill(c: tk.Canvas, W: int, H: int, colour: str) -> None:
        c.configure(bg=colour)

    def _gradient(self, c: tk.Canvas, W: int, H: int) -> None:
        c.configure(bg="black")
        steps = min(W, 512)
        inv_steps = 1.0 / steps
        for i in range(steps):
            v = int(i * inv_steps * 255)
            col = f"#{v:02x}{v:02x}{v:02x}"
            x0 = int(i * W * inv_steps)
            x1 = int((i + 1) * W * inv_steps)
            c.create_rectangle(x0, 0, x1, H, fill=col, outline="")

    def _gamma_patches(self, c: tk.Canvas, W: int, H: int) -> None:
        c.configure(bg="#111111")
        levels = [0, 18, 38, 64, 96, 128, 160, 192, 224, 255]
        cols   = len(levels)
        pw     = W // cols
        for idx, v in enumerate(levels):
            col = f"#{v:02x}{v:02x}{v:02x}"
            x0  = idx * pw
            c.create_rectangle(x0, H // 4, x0 + pw, 3 * H // 4, fill=col, outline="")
            label_col = "#000000" if v > 180 else "#ffffff"
            c.create_text(
                x0 + pw // 2, 3 * H // 4 + 20,
                text=str(v), fill=label_col, font=("Courier", 10),
            )

    def _scrolling_bars(self, c: tk.Canvas, W: int, H: int) -> None:
        """Animate UFO-style horizontal scrolling bars at three speeds."""
        c.configure(bg="#111111")
        speeds = [2, 5, 10]  # pixels per frame
        bar_h  = H // 6
        for row, speed in enumerate(speeds):
            y0 = row * 2 * bar_h + bar_h // 2
            y1 = y0 + bar_h
            # Draw bar at current offset
            x = (self._scroll_x * speed) % (W + 80) - 80
            c.create_rectangle(x, y0, x + 80, y1, fill="#ffffff", outline="")
            c.create_text(
                10, (y0 + y1) // 2,
                text=f"{speed}px/frame",
                fill="#aaaaaa", font=("Courier", 11), anchor="w",
            )
        self._scroll_x += 1
        self._after_id = self.after(16, self._draw)  # ~60 fps

    def _colour_checker(self, c: tk.Canvas, W: int, H: int) -> None:
        c.configure(bg="#222222")
        cols_n = 6
        rows_n = 5
        sw  = W // cols_n
        sh  = H // rows_n
        for idx, (r, g, b, label) in enumerate(self._CHECKER_COLORS):
            row = idx // cols_n
            col = idx  % cols_n
            x0  = col * sw
            y0  = row * sh
            col_hex = f"#{r:02x}{g:02x}{b:02x}"
            c.create_rectangle(x0, y0, x0 + sw, y0 + sh, fill=col_hex, outline="#000")
            text_v = int(0.299 * r + 0.587 * g + 0.114 * b)
            text_col = "#000000" if text_v > 140 else "#ffffff"
            c.create_text(
                x0 + sw // 2, y0 + sh // 2,
                text=label, fill=text_col, font=("Helvetica", 9),
            )


# ===========================================================================
# Frame-pacing test window
# ===========================================================================

class FramePaceWindow(tk.Toplevel):
    """Fullscreen frame-pacing / frame-counter test."""

    def __init__(self, master: tk.Misc) -> None:
        super().__init__(master)
        self._frame    = 0
        self._t0       = time.perf_counter()
        self._bar_x    = 0
        self._after_id: Optional[str] = None

        self.attributes("-fullscreen", True)
        self.configure(bg="black")
        self.bind("<Escape>", lambda _e: self._close())

        self._canvas = tk.Canvas(self, bg="black", highlightthickness=0)
        self._canvas.pack(fill="both", expand=True)
        self._draw()

    def _close(self) -> None:
        if self._after_id:
            self.after_cancel(self._after_id)
        self.destroy()

    def _draw(self) -> None:
        c = self._canvas
        c.delete("all")
        W = self.winfo_screenwidth()
        H = self.winfo_screenheight()

        # Moving bar
        bar_w = 60
        self._bar_x = (self._bar_x + 4) % (W + bar_w)
        c.create_rectangle(
            self._bar_x - bar_w, 0, self._bar_x, H // 3,
            fill="#00cc44", outline="",
        )

        # Frame counter and measured FPS
        elapsed = time.perf_counter() - self._t0
        fps = self._frame / elapsed if elapsed > 0 else 0.0
        self._frame += 1

        c.create_text(W // 2, H // 2,
                      text=f"Frame: {self._frame}\nMeasured FPS: {fps:.1f}",
                      fill="#ffffff", font=("Courier", 24, "bold"),
                      justify="center")
        c.create_text(W // 2, H - 30,
                      text="Press Esc to close",
                      fill="#888888", font=("Helvetica", 12))

        self._after_id = self.after(16, self._draw)


# ===========================================================================
# Main application
# ===========================================================================

class DisplayEvalApp(tk.Tk):
    """Tabbed display evaluation application."""

    _PADX = 10
    _PADY = 5

    def __init__(self) -> None:
        super().__init__()
        self.title("Display Evaluator")
        self.resizable(True, True)
        self.minsize(680, 520)

        # Collect display info (may be partially empty on unsupported platforms)
        self._info: Dict[str, Any] = di.get_display_info()
        self._scores: List[Dict[str, Any]] = di.scorecard(self._info)

        # Per-pattern ratings & notes  {pattern_id: {"rating": IntVar, "notes": StringVar}}
        self._pattern_data: Dict[str, Dict[str, Any]] = {
            pid: {
                "rating": tk.IntVar(value=0),
                "notes":  tk.StringVar(value=""),
            }
            for pid, *_ in _PATTERNS
        }

        self._build_ui()

    # ------------------------------------------------------------------
    # Top-level UI
    # ------------------------------------------------------------------

    def _build_ui(self) -> None:
        outer = ttk.Frame(self, padding=12)
        outer.pack(fill="both", expand=True)

        title_font = font.Font(family="Helvetica", size=15, weight="bold")
        ttk.Label(outer, text="Display Evaluator", font=title_font).pack(
            pady=(0, 8)
        )

        nb = ttk.Notebook(outer)
        nb.pack(fill="both", expand=True)

        nb.add(self._build_info_tab(nb),    text=" 1 · Self-Reported Info ")
        nb.add(self._build_eval_tab(nb),    text=" 2 · Visual Evaluation ")
        nb.add(self._build_refresh_tab(nb), text=" 3 · Refresh & Sync ")
        nb.add(self._build_hdr_tab(nb),     text=" 4 · HDR & Colour ")
        nb.add(self._build_score_tab(nb),   text=" 5 · Scorecard ")

    # ------------------------------------------------------------------
    # Tab 1 – Self-Reported Info
    # ------------------------------------------------------------------

    def _build_info_tab(self, parent: ttk.Notebook) -> ttk.Frame:
        frame = ttk.Frame(parent, padding=10)

        # Warning banner
        warn_font = font.Font(family="Helvetica", size=10, weight="bold")
        warn_frame = tk.Frame(frame, bg="#fff3cd", relief="flat", bd=1)
        warn_frame.pack(fill="x", pady=(0, 10))
        tk.Label(
            warn_frame,
            text="⚠️  All values below are self-reported by the manufacturer or OS and "
                 "have NOT been independently verified with calibration hardware.",
            bg="#fff3cd", fg="#856404",
            font=warn_font,
            wraplength=620, justify="left", padx=8, pady=6,
        ).pack(fill="x")

        # Scrollable content area
        canvas = tk.Canvas(frame, highlightthickness=0)
        scrollbar = ttk.Scrollbar(frame, orient="vertical", command=canvas.yview)
        canvas.configure(yscrollcommand=scrollbar.set)
        scrollbar.pack(side="right", fill="y")
        canvas.pack(side="left", fill="both", expand=True)

        inner = ttk.Frame(canvas)
        inner.bind(
            "<Configure>",
            lambda e: canvas.configure(scrollregion=canvas.bbox("all")),
        )
        canvas.create_window((0, 0), window=inner, anchor="nw")

        def _row(section: str, label: str, value: Any, row: int) -> None:
            if section:
                ttk.Label(inner, text=section,
                          font=font.Font(family="Helvetica", size=10, weight="bold"),
                          foreground="#333366").grid(
                    row=row, column=0, columnspan=2,
                    sticky="w", padx=self._PADX, pady=(10, 2)
                )
                row += 1
            disp = "—" if value is None else str(value)
            ttk.Label(inner, text=label + ":").grid(
                row=row, column=0, sticky="w", padx=(self._PADX + 16, 6), pady=2
            )
            ttk.Label(inner, text=disp, foreground="#333").grid(
                row=row, column=1, sticky="w", padx=6, pady=2
            )

        inf = self._info
        r = 0

        # ── System ──────────────────────────────────────────────────
        _row("System", "Platform", inf.get("platform"), r);   r += 2
        _row("", "Resolution",
             f"{inf['resolution_width']} × {inf['resolution_height']} px"
             if inf.get("resolution_width") else None, r);    r += 1
        _row("", "Refresh Rate",
             f"{inf['refresh_rate']:.2f} Hz" if inf.get("refresh_rate") else None, r)
        r += 1

        # ── Adaptive Sync ────────────────────────────────────────────
        _row("Adaptive Sync", "Supported",
             "Yes" if inf.get("adaptive_sync") else
             ("No" if inf.get("adaptive_sync") is False else None), r);  r += 2
        _row("", "Sync Range", inf.get("adaptive_sync_range"), r);       r += 1

        # ── Colour ───────────────────────────────────────────────────
        p3   = inf.get("gamut_p3_pct")
        srgb = inf.get("gamut_srgb_pct")
        argb = inf.get("gamut_adobergb_pct")
        _row("Colour Gamut (from EDID chromaticity)",
             "sRGB coverage",
             f"{srgb:.1f}%" if srgb is not None else None, r); r += 2
        _row("", "DCI-P3 coverage",
             f"{p3:.1f}%" if p3 is not None else None, r);     r += 1
        _row("", "Adobe RGB coverage",
             f"{argb:.1f}%" if argb is not None else None, r); r += 1

        # Chromaticity coords
        for ch, label in [
            ("color_rx", "Red X"),   ("color_ry", "Red Y"),
            ("color_gx", "Green X"), ("color_gy", "Green Y"),
            ("color_bx", "Blue X"),  ("color_by", "Blue Y"),
            ("color_wx", "White X"), ("color_wy", "White Y"),
        ]:
            v = inf.get(ch)
            _row("", label, f"{v:.4f}" if v is not None else None, r)
            r += 1

        # ── HDR ──────────────────────────────────────────────────────
        _row("HDR", "Reported Tier", inf.get("hdr_tier"), r);   r += 2
        _row("", "Active HDR Mode", inf.get("hdr_active"), r);  r += 1

        # ── ICC / Colour Profile ─────────────────────────────────────
        _row("Colour Profile", "Active ICC Profile Name",
             inf.get("icc_profile_name"), r);                  r += 2
        _row("", "Profile Path", inf.get("icc_profile_path"), r); r += 1
        if inf.get("platform") == "Darwin":
            _row("", "True Tone / Ambient",
                 "On" if inf.get("true_tone") else
                 ("Off" if inf.get("true_tone") is False else None), r)
            r += 1

        # ── EDID ─────────────────────────────────────────────────────
        _row("EDID", "Manufacturer", inf.get("manufacturer_name") or
             inf.get("manufacturer_id"), r);                   r += 2
        _row("", "Monitor Name",   inf.get("monitor_name"), r);   r += 1
        _row("", "Product Code",   inf.get("product_code"), r);   r += 1
        _row("", "Serial Number",  inf.get("serial_number"), r);  r += 1
        _row("", "Panel Interface", inf.get("panel_type"), r);    r += 1
        week = inf.get("manufacture_week")
        year = inf.get("manufacture_year")
        mfr_date = (
            f"Week {week}, {year}" if week else str(year) if year else None
        )
        _row("", "Manufacture Date", mfr_date, r);               r += 1

        return frame

    # ------------------------------------------------------------------
    # Tab 2 – Visual Evaluation
    # ------------------------------------------------------------------

    def _build_eval_tab(self, parent: ttk.Notebook) -> ttk.Frame:
        frame = ttk.Frame(parent, padding=10)

        ttk.Label(
            frame,
            text="Launch each test pattern fullscreen, observe it, then record "
                 "your score (1 = poor … 5 = excellent) and optional notes.",
            wraplength=640, justify="left",
        ).pack(anchor="w", pady=(0, 8))

        for pid, label, desc in _PATTERNS:
            self._add_pattern_row(frame, pid, label, desc)

        return frame

    def _add_pattern_row(
        self, parent: ttk.Frame, pid: str, label: str, desc: str
    ) -> None:
        row_frame = ttk.LabelFrame(parent, text=label, padding=(8, 4))
        row_frame.pack(fill="x", pady=4)

        ttk.Label(row_frame, text=desc, wraplength=500,
                  foreground=_GRAY, justify="left").grid(
            row=0, column=0, columnspan=6, sticky="w", padx=4, pady=(0, 4)
        )

        ttk.Button(
            row_frame, text="▶  Launch Pattern",
            command=lambda p=pid: PatternWindow(self, p),
        ).grid(row=1, column=0, padx=(0, 12), pady=2)

        ttk.Label(row_frame, text="Rating (1–5):").grid(row=1, column=1, padx=4)
        rating_var = self._pattern_data[pid]["rating"]
        for val in range(1, 6):
            ttk.Radiobutton(row_frame, text=str(val),
                            variable=rating_var, value=val).grid(
                row=1, column=1 + val, padx=2
            )

        ttk.Label(row_frame, text="Notes:").grid(row=2, column=0, sticky="w", padx=4)
        notes_entry = ttk.Entry(row_frame, textvariable=self._pattern_data[pid]["notes"],
                                width=60)
        notes_entry.grid(row=2, column=1, columnspan=6, sticky="ew", pady=2)

    # ------------------------------------------------------------------
    # Tab 3 – Refresh & Sync
    # ------------------------------------------------------------------

    def _build_refresh_tab(self, parent: ttk.Notebook) -> ttk.Frame:
        frame = ttk.Frame(parent, padding=12)
        inf   = self._info

        # Data section
        data_frame = ttk.LabelFrame(frame, text="Reported Values", padding=8)
        data_frame.pack(fill="x", pady=(0, 10))

        rows = [
            ("Current Refresh Rate",
             f"{inf['refresh_rate']:.2f} Hz ⚠️" if inf.get("refresh_rate") else "Unknown ⚠️"),
            ("Adaptive Sync",
             "Supported ⚠️" if inf.get("adaptive_sync") else
             ("Not detected ⚠️" if inf.get("adaptive_sync") is False else "Unknown ⚠️")),
            ("Sync Range",
             f"{inf.get('adaptive_sync_range')} ⚠️"
             if inf.get("adaptive_sync_range") else "Unknown ⚠️"),
            ("EDID Min Refresh",
             f"{inf['min_refresh_hz']} Hz" if inf.get("min_refresh_hz") else "—"),
            ("EDID Max Refresh",
             f"{inf['max_refresh_hz']} Hz" if inf.get("max_refresh_hz") else "—"),
        ]
        for r_idx, (lbl, val) in enumerate(rows):
            ttk.Label(data_frame, text=lbl + ":").grid(
                row=r_idx, column=0, sticky="w", padx=8, pady=3
            )
            ttk.Label(data_frame, text=val, foreground="#333").grid(
                row=r_idx, column=1, sticky="w", padx=8, pady=3
            )

        # Frame-pacing test
        fp_frame = ttk.LabelFrame(frame, text="Frame-Pacing Test", padding=8)
        fp_frame.pack(fill="x")

        ttk.Label(
            fp_frame,
            text="Launch the fullscreen frame-pacing test.  A green bar scrolls "
                 "across the screen.  If motion appears jerky or stuttery, your "
                 "display may have refresh-rate or VRR issues.\n"
                 "The frame counter increments every rendered frame; the FPS "
                 "readout shows the measured render rate.",
            wraplength=600, justify="left",
        ).pack(anchor="w", pady=(0, 8))

        ttk.Button(
            fp_frame, text="▶  Launch Frame-Pacing Test",
            command=lambda: FramePaceWindow(self),
        ).pack(anchor="w")

        return frame

    # ------------------------------------------------------------------
    # Tab 4 – HDR & Colour
    # ------------------------------------------------------------------

    def _build_hdr_tab(self, parent: ttk.Notebook) -> ttk.Frame:
        frame = ttk.Frame(parent, padding=12)
        inf   = self._info

        # HDR section
        hdr_frame = ttk.LabelFrame(frame, text="HDR Status", padding=8)
        hdr_frame.pack(fill="x", pady=(0, 10))

        hdr_rows = [
            ("Reported HDR Tier",
             str(inf.get("hdr_tier") or "Not detected") + " ⚠️"),
            ("Active HDR Mode",
             str(inf.get("hdr_active") or "Not detected") + " ⚠️"),
        ]
        for r_idx, (lbl, val) in enumerate(hdr_rows):
            ttk.Label(hdr_frame, text=lbl + ":").grid(
                row=r_idx, column=0, sticky="w", padx=8, pady=3
            )
            ttk.Label(hdr_frame, text=val, foreground="#333").grid(
                row=r_idx, column=1, sticky="w", padx=8, pady=3
            )

        tk.Label(
            hdr_frame,
            text="⚠️  HDR tier is declared by the manufacturer and has not been "
                 "independently verified.",
            fg=_WARN, bg=self.cget("bg"), font=("Helvetica", 9),
            wraplength=580, justify="left",
        ).grid(row=len(hdr_rows), column=0, columnspan=2,
               sticky="w", padx=8, pady=(4, 0))

        # Colour pipeline section
        col_frame = ttk.LabelFrame(frame, text="Colour Pipeline", padding=8)
        col_frame.pack(fill="x")

        col_rows = [
            ("Active ICC Profile",
             str(inf.get("icc_profile_name") or "Not detected") + " ⚠️"),
            ("Profile Path",
             str(inf.get("icc_profile_path") or "—")),
            ("Panel Interface",
             str(inf.get("panel_type") or "—") + " ⚠️"),
        ]
        if inf.get("platform") == "Darwin":
            col_rows.append((
                "True Tone / Ambient Adaptation",
                ("On" if inf.get("true_tone") else
                 "Off" if inf.get("true_tone") is False else "Unknown") + " ⚠️",
            ))

        for r_idx, (lbl, val) in enumerate(col_rows):
            ttk.Label(col_frame, text=lbl + ":").grid(
                row=r_idx, column=0, sticky="w", padx=8, pady=3
            )
            ttk.Label(col_frame, text=val, foreground="#333",
                      wraplength=420).grid(
                row=r_idx, column=1, sticky="w", padx=8, pady=3
            )

        tk.Label(
            col_frame,
            text="⚠️  ICC profile and colour space reported by the OS; accuracy "
                 "depends on the profile quality and display calibration status.",
            fg=_WARN, bg=self.cget("bg"), font=("Helvetica", 9),
            wraplength=580, justify="left",
        ).grid(row=len(col_rows), column=0, columnspan=2,
               sticky="w", padx=8, pady=(4, 0))

        return frame

    # ------------------------------------------------------------------
    # Tab 5 – Scorecard
    # ------------------------------------------------------------------

    def _build_score_tab(self, parent: ttk.Notebook) -> ttk.Frame:
        frame = ttk.Frame(parent, padding=12)

        title_font = font.Font(family="Helvetica", size=11, weight="bold")
        ttk.Label(frame, text="Apple Display Reference Scorecard",
                  font=title_font).pack(anchor="w", pady=(0, 6))
        ttk.Label(
            frame,
            text="⚠️  All self-reported values are unverified.  "
                 "'Hardware Verified' rows require a colorimeter.",
            foreground=_WARN, wraplength=640, justify="left",
        ).pack(anchor="w", pady=(0, 8))

        # Table
        columns = ("Metric", "Reported Value", "Target", "Result", "Note")
        tree = ttk.Treeview(frame, columns=columns, show="headings",
                            height=len(self._scores))
        tree.pack(fill="both", expand=True)

        col_widths = (180, 180, 180, 70, 240)
        for col, w in zip(columns, col_widths):
            tree.heading(col, text=col)
            tree.column(col, width=w, anchor="w", stretch=False)

        # Tags for colouring
        tree.tag_configure("PASS",   foreground=_GREEN)
        tree.tag_configure("REVIEW", foreground=_ORANGE)
        tree.tag_configure("FAIL",   foreground=_RED)
        tree.tag_configure("STUB",   foreground=_GRAY)

        for row in self._scores:
            tag  = row["result"] if row["result"] in ("PASS", "REVIEW", "FAIL") else "STUB"
            tree.insert(
                "", "end",
                values=(
                    row["metric"],
                    row["value"],
                    row["target"],
                    row["result"],
                    row["note"],
                ),
                tags=(tag,),
            )

        # Scrollbar
        sb = ttk.Scrollbar(frame, orient="horizontal", command=tree.xview)
        tree.configure(xscrollcommand=sb.set)
        sb.pack(fill="x")

        # Visual evaluation summary
        eval_frame = ttk.LabelFrame(frame, text="Visual Evaluation Summary", padding=8)
        eval_frame.pack(fill="x", pady=(12, 0))

        self._eval_summary_label = ttk.Label(
            eval_frame,
            text="No ratings recorded yet.  "
                 "Complete patterns in the Visual Evaluation tab, then refresh.",
            foreground=_GRAY, wraplength=600,
        )
        self._eval_summary_label.pack(anchor="w")

        ttk.Button(
            eval_frame, text="↻  Refresh Visual Summary",
            command=self._refresh_eval_summary,
        ).pack(anchor="w", pady=(4, 0))

        return frame

    def _refresh_eval_summary(self) -> None:
        """Update the visual evaluation summary from recorded ratings."""
        lines: List[str] = []
        for pid, plabel, _ in _PATTERNS:
            rating = self._pattern_data[pid]["rating"].get()
            notes  = self._pattern_data[pid]["notes"].get().strip()
            if rating > 0:
                line = f"• {plabel}: {rating}/5"
                if notes:
                    line += f" — {notes}"
                lines.append(line)

        if lines:
            self._eval_summary_label.configure(
                text="\n".join(lines), foreground="#333"
            )
        else:
            self._eval_summary_label.configure(
                text="No ratings recorded yet.",
                foreground=_GRAY,
            )


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main() -> None:
    app = DisplayEvalApp()
    app.mainloop()


if __name__ == "__main__":
    main()
