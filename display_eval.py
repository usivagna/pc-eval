"""
Display Evaluator
=================
A simple desktop GUI that auto-detects your display's specs, highlights key
values, and scores them against Apple's Retina Display reference targets.

When multiple monitors are connected each display gets its own tab with an
independent scorecard.

Run with:
    python display_eval.py
"""

from __future__ import annotations

import math
import tkinter as tk
from tkinter import font, ttk
from typing import Any, Dict, List

import display_info as di

# ---------------------------------------------------------------------------
# Constants
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


def _retina_min_ppi(distance_in: float) -> float:
    return 1.0 / (distance_in * math.tan(_ARCMINUTE_RAD))


def _calculate_ppi(w: int, h: int, diag: float) -> float:
    return math.sqrt(w ** 2 + h ** 2) / diag


def _score_label(pct: float) -> str:
    """Map a 0-100 percentage to a letter grade."""
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


# ===========================================================================
# Per-display scorecard panel
# ===========================================================================

class _DisplayCard(ttk.Frame):
    """Self-contained scorecard panel for a single display.

    Embeds the "Detected Display Info", "Your Setup", and "Scorecard" sections
    for one monitor.  Multiple instances can coexist inside a
    ``ttk.Notebook`` for multi-monitor setups.
    """

    _PADX = 14
    _PADY = 5

    def __init__(self, parent: tk.Widget, info: Dict[str, Any]) -> None:
        super().__init__(parent)
        self._info     = info
        self._screen_w = info.get("resolution_width")  or 0
        self._screen_h = info.get("resolution_height") or 0
        self._build_ui()
        self._update_scores()

    # ------------------------------------------------------------------
    # UI
    # ------------------------------------------------------------------

    def _build_ui(self) -> None:
        outer = ttk.Frame(self, padding=10)
        outer.grid(row=0, column=0, sticky="nsew")

        # ── Detected values ───────────────────────────────────────────
        info_frame = ttk.LabelFrame(outer, text="  Detected Display Info  ", padding=10)
        info_frame.grid(row=0, column=0, columnspan=3, sticky="ew", pady=(0, 10))

        val_font = font.Font(family="Helvetica", size=11, weight="bold")
        inf = self._info

        detected_rows = [
            ("Monitor", inf.get("monitor_name") or inf.get("manufacturer_name")
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
             f"{inf['gamut_p3_pct']:.1f}%" if inf.get("gamut_p3_pct") is not None
             else "Unknown"),
            ("sRGB Gamut",
             f"{inf['gamut_srgb_pct']:.1f}%" if inf.get("gamut_srgb_pct") is not None
             else "Unknown"),
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

        # ── Screen diagonal + viewing distance ────────────────────────
        input_frame = ttk.LabelFrame(outer, text="  Your Setup  ", padding=10)
        input_frame.grid(row=1, column=0, columnspan=3, sticky="ew", pady=(0, 10))

        ttk.Label(input_frame, text="Screen diagonal (inches):").grid(
            row=0, column=0, sticky="w", padx=(4, 8), pady=self._PADY,
        )
        self._diagonal_var = tk.StringVar(value="")
        diag_entry = ttk.Entry(input_frame, textvariable=self._diagonal_var, width=10)
        diag_entry.grid(row=0, column=1, sticky="w", pady=self._PADY)
        diag_entry.bind("<KeyRelease>", lambda _e: self._update_scores())

        ttk.Label(input_frame, text="Viewing distance:").grid(
            row=1, column=0, sticky="w", padx=(4, 8), pady=self._PADY,
        )
        distance_labels = [label for label, _ in VIEWING_DISTANCES]
        self._distance_combo = ttk.Combobox(
            input_frame, values=distance_labels, state="readonly", width=30,
        )
        default_idx = next(
            (i for i, (_, d) in enumerate(VIEWING_DISTANCES) if d == 18), 3
        )
        self._distance_combo.current(default_idx)
        self._distance_combo.grid(row=1, column=1, sticky="w", pady=self._PADY)
        self._distance_combo.bind("<<ComboboxSelected>>", lambda _e: self._update_scores())

        # ── Scorecard ─────────────────────────────────────────────────
        score_frame = ttk.LabelFrame(
            outer, text="  Scorecard vs Apple Retina Targets  ", padding=10
        )
        score_frame.grid(row=2, column=0, columnspan=3, sticky="ew", pady=(0, 10))

        header_font = font.Font(family="Helvetica", size=9, weight="bold")
        for ci, hdr in enumerate(("Metric", "Your Value", "Apple Target", "Result")):
            ttk.Label(score_frame, text=hdr, font=header_font).grid(
                row=0, column=ci, sticky="w", padx=8, pady=(0, 4),
            )
        ttk.Separator(score_frame, orient="horizontal").grid(
            row=1, column=0, columnspan=4, sticky="ew", pady=2,
        )

        self._score_widgets: List[Dict[str, tk.Label]] = []

        metric_names = [
            "Pixel Density (PPI)",
            "DCI-P3 Gamut",
            "Refresh Rate",
            "HDR Support",
        ]
        for i, name in enumerate(metric_names):
            r = i + 2
            lbl_metric = tk.Label(score_frame, text=name, anchor="w")
            lbl_value  = tk.Label(score_frame, text="—", anchor="w")
            lbl_target = tk.Label(score_frame, text="—", anchor="w", fg=_GRAY)
            lbl_result = tk.Label(score_frame, text="—", anchor="w",
                                  font=font.Font(family="Helvetica", size=10,
                                                 weight="bold"))
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

        # ── Overall score ─────────────────────────────────────────────
        ttk.Separator(outer, orient="horizontal").grid(
            row=3, column=0, columnspan=3, sticky="ew", pady=8,
        )

        self._overall_font = font.Font(family="Helvetica", size=28, weight="bold")
        self._overall_label = tk.Label(
            outer, text="—", font=self._overall_font, fg=_GRAY,
        )
        self._overall_label.grid(row=4, column=0, columnspan=3, pady=(0, 2))

        self._overall_desc = tk.Label(
            outer, text="Enter screen diagonal to get your score",
            fg=_GRAY, font=font.Font(family="Helvetica", size=11),
        )
        self._overall_desc.grid(row=5, column=0, columnspan=3, pady=(0, 8))

    # ------------------------------------------------------------------
    # Score computation
    # ------------------------------------------------------------------

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
        ppi_target = f">= {min_ppi:.0f} PPI @ {distance:.0f} in"
        ppi_w["target"].configure(text=ppi_target)

        raw_diag = self._diagonal_var.get().strip()
        ppi = None
        if raw_diag:
            try:
                diagonal = float(raw_diag)
                if diagonal > 0 and self._screen_w and self._screen_h:
                    ppi = _calculate_ppi(self._screen_w, self._screen_h, diagonal)
            except ValueError:
                pass

        if ppi is not None:
            ppi_pct = min(ppi / min_ppi * 100, 100)
            scores.append(ppi_pct)
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
            p3_pct = min(p3 / p3_target * 100, 100)
            scores.append(p3_pct)
            result = "PASS" if p3 >= p3_target else ("REVIEW" if p3 >= 80 else "FAIL")
            color = _GREEN if result == "PASS" else (_ORANGE if result == "REVIEW" else _RED)
            p3_w["value"].configure(text=f"{p3:.1f}%")
            p3_w["result"].configure(text=result, fg=color)
        else:
            p3_w["value"].configure(text="Unknown")
            p3_w["result"].configure(text="—", fg=_GRAY)

        # --- Refresh Rate ---
        rr_w = self._score_widgets[2]
        tgt_hz = 120
        rr_w["target"].configure(text=f">= {tgt_hz} Hz")
        rr = inf.get("refresh_rate") or inf.get("max_refresh_hz")
        if rr is not None:
            rr_pct = min(rr / tgt_hz * 100, 100)
            scores.append(rr_pct)
            result = "PASS" if rr >= tgt_hz else ("REVIEW" if rr >= 60 else "FAIL")
            color = _GREEN if result == "PASS" else (_ORANGE if result == "REVIEW" else _RED)
            rr_w["value"].configure(text=f"{rr:.0f} Hz")
            rr_w["result"].configure(text=result, fg=color)
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

        # --- Overall ---
        if scores:
            overall = sum(scores) / len(scores)
            grade = _score_label(overall)
            self._overall_label.configure(
                text=f"{grade}  ({overall:.0f}%)",
                fg=_score_color(overall),
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

class DisplayEvalApp(tk.Tk):

    def __init__(self) -> None:
        super().__init__()
        self.title("Display Evaluator – Apple Retina Scorecard")
        self.resizable(False, False)

        all_displays = di.get_all_displays_info()
        if not all_displays:
            all_displays = [di.get_display_info()]

        self._build_ui(all_displays)

    # ------------------------------------------------------------------
    # UI
    # ------------------------------------------------------------------

    def _build_ui(self, all_displays: List[Dict[str, Any]]) -> None:
        outer = ttk.Frame(self, padding=18)
        outer.grid(row=0, column=0, sticky="nsew")

        # Title
        title_font = font.Font(family="Helvetica", size=16, weight="bold")
        ttk.Label(outer, text="Display Evaluator", font=title_font).grid(
            row=0, column=0, columnspan=3, pady=(0, 4),
        )
        ttk.Label(
            outer, text="How does your display compare to Apple Retina standards?",
            foreground=_GRAY,
        ).grid(row=1, column=0, columnspan=3, pady=(0, 12))

        if len(all_displays) > 1:
            # Multiple monitors: one tab per display
            nb = ttk.Notebook(outer)
            nb.grid(row=2, column=0, columnspan=3, sticky="nsew")
            for idx, disp_info in enumerate(all_displays):
                tab_name = (
                    disp_info.get("monitor_name")
                    or disp_info.get("manufacturer_name")
                    or f"Display {idx + 1}"
                )
                card = _DisplayCard(nb, disp_info)
                nb.add(card, text=f"  {tab_name}  ")
        else:
            # Single monitor: show card directly (no tab bar overhead)
            card = _DisplayCard(outer, all_displays[0])
            card.grid(row=2, column=0, columnspan=3)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

def main() -> None:
    app = DisplayEvalApp()
    app.mainloop()


if __name__ == "__main__":
    main()
