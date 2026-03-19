"""
PC Evaluator
============
A tabbed desktop GUI that scores your system against Apple standards:

  Tab 1 – Display    : auto-detects all connected display specs and scores each
                       independently against Apple Retina Display reference targets.
                       When multiple monitors are connected a sub-tab is shown
                       for each display.
  Tab 2 – Processor  : auto-detects CPU / SoC and scores it across seven
                       dimensions vs the Apple M3 Pro baseline.

Run with:
    python display_eval.py
"""

from __future__ import annotations

import math
import re
import tkinter as tk
from tkinter import font, ttk
from typing import Any, Dict, List, Optional

import display_info as di
import processor_info as pi

# ---------------------------------------------------------------------------
# Shared constants
# ---------------------------------------------------------------------------
_GREEN  = "#007a00"
_ORANGE = "#b85c00"
_RED    = "#cc0000"
_GRAY   = "#555555"

_ARCMINUTE_RAD = math.pi / 10800

VIEWING_DISTANCES = [
    ("10 in  – phone held close", 10),
    ("12 in  – tablet", 12),
    ("15 in  – laptop", 15),
    ("18 in  – laptop / small monitor", 18),
    ("20 in  – desktop monitor", 20),
    ("24 in  – large desktop monitor", 24),
    ("30 in  – large/TV monitor", 30),
]


# ---------------------------------------------------------------------------
# Shared helpers
# ---------------------------------------------------------------------------

def _retina_min_ppi(distance_in: float) -> float:
    return 1.0 / (distance_in * math.tan(_ARCMINUTE_RAD))


def _calculate_ppi(w: int, h: int, diag: float) -> float:
    return math.sqrt(w ** 2 + h ** 2) / diag


def _score_label(pct: float) -> str:
    """Map a 0–100 percentage to a letter grade."""
    if pct >= 90:
        return "A"
    if pct >= 75:
        return "B"
    if pct >= 60:
        return "C"
    if pct >= 40:
        return "D"
    return "F"


def _score_color(pct: float) -> str:
    if pct >= 75:
        return _GREEN
    if pct >= 50:
        return _ORANGE
    return _RED


def _result_color(result: str) -> str:
    if result == "PASS":
        return _GREEN
    if result == "REVIEW":
        return _ORANGE
    if result == "FAIL":
        return _RED
    return _GRAY


# ===========================================================================
# Per-display scorecard panel
# ===========================================================================

class _DisplayCard(ttk.Frame):
    """Self-contained scorecard panel for a single display.

    Embeds the "Detected Display Info", "Your Setup", and "Scorecard" sections
    for one monitor.  Multiple instances can coexist inside a sub-notebook
    within the Display tab for multi-monitor setups.
    """

    _PADY = 5

    def __init__(self, parent: tk.Widget, info: Dict[str, Any]) -> None:
        super().__init__(parent, padding=10)
        self._info = info
        # Fall back to tkinter's own screen detection when the OS/EDID
        # collection did not return a resolution.
        self._screen_w = (
            info.get("resolution_width") or self.winfo_screenwidth()
        )
        self._screen_h = (
            info.get("resolution_height") or self.winfo_screenheight()
        )
        self._build_ui()
        self._update_scores()

    def _build_ui(self) -> None:
        val_font    = font.Font(family="Helvetica", size=11, weight="bold")
        header_font = font.Font(family="Helvetica", size=9, weight="bold")
        inf = self._info

        # ── Detected values ───────────────────────────────────────────
        info_frame = ttk.LabelFrame(self, text="  Detected Display Info  ", padding=10)
        info_frame.grid(row=0, column=0, columnspan=3, sticky="ew", pady=(0, 10))

        detected_rows = [
            ("Monitor",
             inf.get("monitor_name") or inf.get("manufacturer_name")
             or inf.get("manufacturer_id") or "Unknown"),
            ("Resolution",
             f"{self._screen_w} x {self._screen_h} px"
             if self._screen_w and self._screen_h else "Unknown"),
            ("Refresh Rate",
             f"{inf['refresh_rate']:.0f} Hz" if inf.get("refresh_rate") else "Unknown"),
            ("Adaptive Sync",
             inf.get("adaptive_sync_range") if inf.get("adaptive_sync")
             else "Not detected"),
            ("DCI-P3 Gamut",
             f"{inf['gamut_p3_pct']:.1f}%"
             if inf.get("gamut_p3_pct") is not None else "Unknown"),
            ("sRGB Gamut",
             f"{inf['gamut_srgb_pct']:.1f}%"
             if inf.get("gamut_srgb_pct") is not None else "Unknown"),
            ("HDR", str(inf.get("hdr_tier") or "Not detected")),
            ("Panel Type", str(inf.get("panel_type") or "Unknown")),
            ("Color Profile", str(inf.get("icc_profile_name") or "Not detected")),
        ]

        for r, (label, value) in enumerate(detected_rows):
            ttk.Label(info_frame, text=label + ":").grid(
                row=r, column=0, sticky="w", padx=(4, 10), pady=2,
            )
            tk.Label(info_frame, text=value, font=val_font, fg="#1a1a1a").grid(
                row=r, column=1, sticky="w", padx=4, pady=2,
            )

        # ── Setup inputs ───────────────────────────────────────────────
        input_frame = ttk.LabelFrame(self, text="  Your Setup  ", padding=10)
        input_frame.grid(row=1, column=0, columnspan=3, sticky="ew", pady=(0, 10))

        ttk.Label(input_frame, text="Screen diagonal (inches):").grid(
            row=0, column=0, sticky="w", padx=(4, 8), pady=self._PADY,
        )
        detected_diag = inf.get("diagonal_inches")
        self._diagonal_var = tk.StringVar(
            value=str(detected_diag) if detected_diag else ""
        )
        diag_entry = ttk.Entry(input_frame, textvariable=self._diagonal_var, width=10)
        diag_entry.grid(row=0, column=1, sticky="w", pady=self._PADY)
        diag_entry.bind("<KeyRelease>", lambda _e: self._update_scores())

        ttk.Label(input_frame, text="Viewing distance:").grid(
            row=1, column=0, sticky="w", padx=(4, 8), pady=self._PADY,
        )
        distance_labels = [lbl for lbl, _ in VIEWING_DISTANCES]
        self._distance_combo = ttk.Combobox(
            input_frame, values=distance_labels, state="readonly", width=30,
        )
        default_idx = next(
            (i for i, (_, d) in enumerate(VIEWING_DISTANCES) if d == 18), 3
        )
        self._distance_combo.current(default_idx)
        self._distance_combo.grid(row=1, column=1, sticky="w", pady=self._PADY)
        self._distance_combo.bind("<<ComboboxSelected>>", lambda _e: self._update_scores())

        # ── Scorecard table ────────────────────────────────────────────
        score_frame = ttk.LabelFrame(
            self, text="  Scorecard vs Apple Retina Targets  ", padding=10
        )
        score_frame.grid(row=2, column=0, columnspan=3, sticky="ew", pady=(0, 10))

        for ci, hdr in enumerate(("Metric", "Your Value", "Apple Target", "Result")):
            ttk.Label(score_frame, text=hdr, font=header_font).grid(
                row=0, column=ci, sticky="w", padx=8, pady=(0, 4),
            )
        ttk.Separator(score_frame, orient="horizontal").grid(
            row=1, column=0, columnspan=4, sticky="ew", pady=2,
        )

        self._score_widgets: List[Dict[str, tk.Label]] = []
        for i, name in enumerate(
            ("Pixel Density (PPI)", "DCI-P3 Gamut", "Refresh Rate", "HDR Support")
        ):
            r = i + 2
            lbl_metric = tk.Label(score_frame, text=name, anchor="w")
            lbl_value  = tk.Label(score_frame, text="—", anchor="w")
            lbl_target = tk.Label(score_frame, text="—", anchor="w", fg=_GRAY)
            lbl_result = tk.Label(
                score_frame, text="—", anchor="w",
                font=font.Font(family="Helvetica", size=10, weight="bold"),
            )
            lbl_metric.grid(row=r, column=0, sticky="w", padx=8, pady=3)
            lbl_value.grid( row=r, column=1, sticky="w", padx=8, pady=3)
            lbl_target.grid(row=r, column=2, sticky="w", padx=8, pady=3)
            lbl_result.grid(row=r, column=3, sticky="w", padx=8, pady=3)
            self._score_widgets.append({
                "metric": lbl_metric,
                "value":  lbl_value,
                "target": lbl_target,
                "result": lbl_result,
            })

        # ── Overall grade ──────────────────────────────────────────────
        ttk.Separator(self, orient="horizontal").grid(
            row=3, column=0, columnspan=3, sticky="ew", pady=8,
        )
        self._overall_font = font.Font(family="Helvetica", size=28, weight="bold")
        self._overall_label = tk.Label(
            self, text="—", font=self._overall_font, fg=_GRAY,
        )
        self._overall_label.grid(row=4, column=0, columnspan=3, pady=(0, 2))
        self._overall_desc = tk.Label(
            self, text="Enter screen diagonal to get your score",
            fg=_GRAY, font=font.Font(family="Helvetica", size=11),
        )
        self._overall_desc.grid(row=5, column=0, columnspan=3, pady=(0, 8))

    def _selected_distance(self) -> float:
        idx = self._distance_combo.current()
        return VIEWING_DISTANCES[idx][1]

    def _update_scores(self) -> None:
        inf = self._info
        distance = self._selected_distance()
        min_ppi = _retina_min_ppi(distance)
        scores: List[float] = []

        # --- PPI ---
        ppi_w = self._score_widgets[0]
        ppi_w["target"].configure(text=f">= {min_ppi:.0f} PPI @ {distance:.0f} in")

        ppi: Optional[float] = None
        raw_diag = self._diagonal_var.get().strip()
        if raw_diag:
            try:
                diagonal = float(raw_diag)
                if diagonal > 0 and self._screen_w and self._screen_h:
                    ppi = _calculate_ppi(self._screen_w, self._screen_h, diagonal)
            except ValueError:
                pass

        if ppi is not None:
            scores.append(min(ppi / min_ppi * 100, 100))
            is_pass = ppi >= min_ppi
            ppi_w["value"].configure(text=f"{ppi:.1f} PPI")
            ppi_w["result"].configure(
                text="PASS" if is_pass else "FAIL",
                fg=_GREEN if is_pass else _RED,
            )
        else:
            ppi_w["value"].configure(text="—")
            ppi_w["result"].configure(text="—", fg=_GRAY)

        # --- DCI-P3 Gamut ---
        p3_w = self._score_widgets[1]
        p3_target = 95.0
        p3_w["target"].configure(text=f">= {p3_target:.0f}%")
        p3 = inf.get("gamut_p3_pct")
        if p3 is not None:
            scores.append(min(p3 / p3_target * 100, 100))
            result = "PASS" if p3 >= p3_target else ("REVIEW" if p3 >= 80 else "FAIL")
            p3_w["value"].configure(text=f"{p3:.1f}%")
            p3_w["result"].configure(text=result, fg=_result_color(result))
        else:
            p3_w["value"].configure(text="Unknown")
            p3_w["result"].configure(text="—", fg=_GRAY)

        # --- Refresh Rate ---
        rr_w = self._score_widgets[2]
        tgt_hz = 120
        rr_w["target"].configure(text=f">= {tgt_hz} Hz")
        rr = inf.get("refresh_rate") or inf.get("max_refresh_hz")
        if rr is not None:
            scores.append(min(rr / tgt_hz * 100, 100))
            result = "PASS" if rr >= tgt_hz else ("REVIEW" if rr >= 60 else "FAIL")
            rr_w["value"].configure(text=f"{rr:.0f} Hz")
            rr_w["result"].configure(text=result, fg=_result_color(result))
        else:
            rr_w["value"].configure(text="Unknown")
            rr_w["result"].configure(text="—", fg=_GRAY)

        # --- HDR ---
        hdr_w = self._score_widgets[3]
        hdr_w["target"].configure(text="HDR supported")
        hdr = inf.get("hdr_tier")
        if hdr:
            scores.append(100)
            hdr_w["value"].configure(text=str(hdr))
            hdr_w["result"].configure(text="PASS", fg=_GREEN)
        else:
            scores.append(0)
            hdr_w["value"].configure(text="Not detected")
            hdr_w["result"].configure(text="FAIL", fg=_RED)

        # --- Overall grade ---
        if scores:
            overall = sum(scores) / len(scores)
            grade = _score_label(overall)
            self._overall_label.configure(
                text=f"{grade}  ({overall:.0f}%)", fg=_score_color(overall),
            )
            if overall >= 90:
                desc = "Excellent — meets or exceeds Apple Retina standards"
            elif overall >= 75:
                desc = "Good — close to Apple Retina quality"
            elif overall >= 50:
                desc = "Fair — some areas fall short of Retina standards"
            else:
                desc = "Below Apple Retina standards"
            self._overall_desc.configure(text=desc, fg=_score_color(overall))
        else:
            self._overall_label.configure(text="—", fg=_GRAY)
            self._overall_desc.configure(
                text="Enter screen diagonal to get your score", fg=_GRAY,
            )


# ===========================================================================
# Main application
# ===========================================================================

class PCEvalApp(tk.Tk):
    """Tabbed application with Display and Processor evaluation tabs."""

    _PADX = 14
    _PADY = 5

    def __init__(self) -> None:
        super().__init__()
        self.title("PC Evaluator – Display & Processor vs Apple Standards")
        self.resizable(False, False)

        # Fetch system info up-front so both tabs share the same snapshot
        self._all_displays: List[Dict[str, Any]] = di.get_all_displays_info()
        self._proc_info: Dict[str, Any] = pi.get_processor_info()

        self._notebook = ttk.Notebook(self)
        self._notebook.pack(fill="both", expand=True)

        self._build_display_tab()
        self._build_processor_tab()

    # ======================================================================
    # Tab 1 – Display
    # ======================================================================

    def _build_display_tab(self) -> None:
        tab = ttk.Frame(self._notebook, padding=18)
        self._notebook.add(tab, text="  Display  ")

        title_font = font.Font(family="Helvetica", size=16, weight="bold")
        ttk.Label(tab, text="Display Evaluator", font=title_font).grid(
            row=0, column=0, columnspan=3, pady=(0, 4),
        )
        ttk.Label(
            tab,
            text="How does your display compare to Apple Retina standards?",
            foreground=_GRAY,
        ).grid(row=1, column=0, columnspan=3, pady=(0, 12))

        if len(self._all_displays) > 1:
            # Multiple monitors: one sub-tab per display
            sub_nb = ttk.Notebook(tab)
            sub_nb.grid(row=2, column=0, columnspan=3, sticky="nsew")
            for idx, disp_info in enumerate(self._all_displays):
                tab_name = (
                    disp_info.get("monitor_name")
                    or disp_info.get("manufacturer_name")
                    or f"Display {idx + 1}"
                )
                card = _DisplayCard(sub_nb, disp_info)
                sub_nb.add(card, text=f"  {tab_name}  ")
        else:
            # Single monitor: embed the card directly
            card = _DisplayCard(tab, self._all_displays[0])
            card.grid(row=2, column=0, columnspan=3)

    # ======================================================================
    # Tab 2 – Processor
    # ======================================================================

    def _build_processor_tab(self) -> None:
        tab = ttk.Frame(self._notebook, padding=18)
        self._notebook.add(tab, text="  Processor  ")

        # Make the tab scrollable via a canvas + scrollbar
        canvas = tk.Canvas(tab, borderwidth=0, highlightthickness=0)
        scrollbar = ttk.Scrollbar(tab, orient="vertical", command=canvas.yview)
        canvas.configure(yscrollcommand=scrollbar.set)

        scrollbar.pack(side="right", fill="y")
        canvas.pack(side="left", fill="both", expand=True)

        inner = ttk.Frame(canvas, padding=(0, 0, 12, 0))
        window_id = canvas.create_window((0, 0), window=inner, anchor="nw")

        def _on_resize(event: tk.Event) -> None:
            canvas.itemconfig(window_id, width=event.width)

        def _on_frame_configure(_event: tk.Event) -> None:
            canvas.configure(scrollregion=canvas.bbox("all"))

        canvas.bind("<Configure>", _on_resize)
        inner.bind("<Configure>", _on_frame_configure)

        # Mouse-wheel scrolling
        def _on_mousewheel(event: tk.Event) -> None:
            canvas.yview_scroll(int(-1 * (event.delta / 120)), "units")

        canvas.bind_all("<MouseWheel>", _on_mousewheel)

        self._build_processor_content(inner)

    def _build_processor_content(self, parent: ttk.Frame) -> None:
        """Populate the processor tab with detected info and scorecard."""
        inf = self._proc_info
        tier = inf.get("tier") or {}

        title_font  = font.Font(family="Helvetica", size=16, weight="bold")
        val_font    = font.Font(family="Helvetica", size=11, weight="bold")
        header_font = font.Font(family="Helvetica", size=9, weight="bold")
        result_font = font.Font(family="Helvetica", size=10, weight="bold")
        badge_font  = font.Font(family="Helvetica", size=9, slant="italic")

        ttk.Label(parent, text="Processor Evaluator", font=title_font).grid(
            row=0, column=0, columnspan=5, pady=(0, 4), sticky="w",
        )
        ttk.Label(
            parent,
            text="How does your CPU / SoC compare to the Apple M3 Pro baseline?",
            foreground=_GRAY,
        ).grid(row=1, column=0, columnspan=5, pady=(0, 12), sticky="w")

        # ── Detected CPU info ──────────────────────────────────────────
        info_frame = ttk.LabelFrame(parent, text="  Detected Processor Info  ", padding=10)
        info_frame.grid(row=2, column=0, columnspan=5, sticky="ew", pady=(0, 10))

        cpu_model   = inf.get("cpu_model") or "Unknown"
        vendor      = inf.get("vendor") or tier.get("vendor") or "Unknown"
        arch        = inf.get("architecture") or tier.get("arch") or "Unknown"
        total_cores = inf.get("total_cores")
        p_cores     = inf.get("performance_cores")
        e_cores     = inf.get("efficiency_cores")
        threads     = inf.get("total_threads")
        max_freq    = inf.get("max_freq_mhz")
        tdp         = tier.get("typical_tdp")
        tier_label  = tier.get("label") or "Unrecognised"

        if p_cores and e_cores:
            cores_str = f"{p_cores}P + {e_cores}E"
        elif total_cores:
            cores_str = str(total_cores)
        else:
            cores_str = "Unknown"

        detected_rows = [
            ("CPU / SoC",           cpu_model),
            ("Recognised Tier",     tier_label),
            ("Vendor",              vendor),
            ("Architecture",        arch),
            ("Cores",               cores_str),
            ("Threads",             str(threads) if threads else "Unknown"),
            ("Max Frequency",       f"{max_freq:.0f} MHz" if max_freq else "Unknown"),
            ("Typical TDP",         f"{tdp} W" if tdp is not None else "Unknown"),
        ]

        for r, (label, value) in enumerate(detected_rows):
            ttk.Label(info_frame, text=label + ":").grid(
                row=r, column=0, sticky="w", padx=(4, 10), pady=2,
            )
            tk.Label(info_frame, text=value, font=val_font, fg="#1a1a1a").grid(
                row=r, column=1, sticky="w", padx=4, pady=2,
            )

        # ── Scorecard table ────────────────────────────────────────────
        score_frame = ttk.LabelFrame(
            parent, text="  Scorecard vs Apple M3 Pro  ", padding=10
        )
        score_frame.grid(row=3, column=0, columnspan=5, sticky="ew", pady=(0, 10))

        col_headers = ("Dimension", "Your Score", "Reference", "Result", "Consumer Insight")
        col_widths  = (26,           18,            14,          8,        42)
        for ci, (hdr, w) in enumerate(zip(col_headers, col_widths)):
            ttk.Label(score_frame, text=hdr, font=header_font, width=w).grid(
                row=0, column=ci, sticky="w", padx=6, pady=(0, 4),
            )
        ttk.Separator(score_frame, orient="horizontal").grid(
            row=1, column=0, columnspan=5, sticky="ew", pady=2,
        )

        rows = pi.scorecard(inf)
        # Only show the 7 dimension rows in the table (not strengths/weaknesses)
        dimension_rows = [rw for rw in rows if rw["result"] in ("PASS", "REVIEW", "FAIL")]

        for i, row in enumerate(dimension_rows):
            r = i + 2
            result = row["result"]
            color  = _result_color(result)
            badge  = row.get("badge", "")
            note   = row.get("note", "")
            insight = f"{badge} — {note}" if badge else note

            tk.Label(score_frame, text=row["metric"],   anchor="w", width=26).grid(
                row=r, column=0, sticky="w", padx=6, pady=3,
            )
            tk.Label(score_frame, text=row["value"],    anchor="w", width=18).grid(
                row=r, column=1, sticky="w", padx=6, pady=3,
            )
            tk.Label(score_frame, text=row["target"],   anchor="w", width=14,
                     fg=_GRAY).grid(row=r, column=2, sticky="w", padx=6, pady=3)
            tk.Label(score_frame, text=result, anchor="w", width=8,
                     font=result_font, fg=color).grid(
                row=r, column=3, sticky="w", padx=6, pady=3,
            )
            tk.Label(score_frame, text=insight, anchor="w", width=42,
                     wraplength=340, justify="left",
                     font=badge_font, fg=_GRAY).grid(
                row=r, column=4, sticky="w", padx=6, pady=3,
            )

        # ── Strengths & weaknesses ─────────────────────────────────────
        sw_rows = [rw for rw in rows if rw["result"] == "—"]

        sw_frame = ttk.LabelFrame(
            parent, text="  Strengths & Weaknesses  ", padding=10
        )
        sw_frame.grid(row=4, column=0, columnspan=5, sticky="ew", pady=(0, 10))

        for sw_row in sw_rows:
            label_color = _GREEN if sw_row["metric"] == "Strengths" else _RED
            tk.Label(sw_frame, text=sw_row["metric"] + ":", width=12,
                     font=header_font, fg=label_color, anchor="nw").grid(
                row=sw_rows.index(sw_row), column=0, sticky="nw", padx=(4, 8), pady=4,
            )
            tk.Label(sw_frame, text=sw_row["value"], anchor="nw",
                     wraplength=480, justify="left").grid(
                row=sw_rows.index(sw_row), column=1, sticky="nw", padx=4, pady=4,
            )

        # ── Overall processor assessment ───────────────────────────────
        ttk.Separator(parent, orient="horizontal").grid(
            row=5, column=0, columnspan=5, sticky="ew", pady=8,
        )

        scored = [
            rw for rw in dimension_rows
            if rw["value"] != "Unknown"
        ]
        if scored:
            # Derive numeric percentages from "XX% of M3 Pro" strings
            pcts: List[float] = []
            for rw in scored:
                val = rw["value"]
                m = re.match(r"(\d+)%", val)
                if m:
                    pcts.append(float(m.group(1)))
            if pcts:
                overall = sum(pcts) / len(pcts)
                grade = _score_label(overall)
                grade_color = _score_color(overall)
                if overall >= 90:
                    verdict = "Comparable to or better than M3 Pro overall"
                elif overall >= 70:
                    verdict = "Competitive in most areas; some gaps vs M3 Pro"
                else:
                    verdict = "Noticeably behind M3 Pro in key dimensions"
            else:
                grade = "—"
                grade_color = _GRAY
                verdict = "Could not compute overall score"
        else:
            grade = "—"
            grade_color = _GRAY
            verdict = "Processor not recognised — install data unavailable"

        overall_font = font.Font(family="Helvetica", size=28, weight="bold")
        tk.Label(parent, text=grade, font=overall_font, fg=grade_color).grid(
            row=6, column=0, columnspan=5, pady=(0, 2),
        )
        tk.Label(
            parent, text=verdict, fg=grade_color,
            font=font.Font(family="Helvetica", size=11),
        ).grid(row=7, column=0, columnspan=5, pady=(0, 8))

        tk.Label(
            parent,
            text="Scores are relative to Apple M3 Pro = 100%.  "
                 "Values are derived from published benchmark data and\n"
                 "manufacturer specifications; not independently verified.",
            fg=_GRAY,
            font=font.Font(family="Helvetica", size=9),
            justify="center",
        ).grid(row=8, column=0, columnspan=5, pady=(0, 12))


# ---------------------------------------------------------------------------
# Backward-compatibility alias
# ---------------------------------------------------------------------------

#: Alias kept for any code that imported ``DisplayEvalApp`` by the old name.
DisplayEvalApp = PCEvalApp


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main() -> None:
    app = PCEvalApp()
    app.mainloop()


if __name__ == "__main__":
    main()
