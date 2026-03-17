"""
Retina Display Checker
Determines whether a display meets Apple's Retina Display definition based on
screen dimensions, pixel density (PPI), and assumed viewing distance.

Apple defines a Retina Display as one where the pixel density is high enough
that the human eye cannot distinguish individual pixels at a normal viewing
distance. The threshold PPI is calculated using the formula:

    min_ppi = 1 / (distance_inches * tan(arcminute_radians))

where arcminute_radians = π / 10800 (one arcminute, the resolving limit of
the human eye).  Simplified: min_ppi ≈ 3438 / distance_inches.
"""

import math
import tkinter as tk
from tkinter import ttk, font

# One arcminute in radians (angular resolution limit of the human eye).
_ARCMINUTE_RAD = math.pi / 10800

# Pre-defined viewing distances (label, inches).
VIEWING_DISTANCES = [
    ("10 in  – phone held close", 10),
    ("12 in  – tablet", 12),
    ("15 in  – laptop", 15),
    ("18 in  – laptop / small monitor", 18),
    ("20 in  – desktop monitor", 20),
    ("24 in  – large desktop monitor", 24),
    ("30 in  – large/TV monitor", 30),
]


def calculate_ppi(width_px: int, height_px: int, diagonal_in: float) -> float:
    """Return pixels-per-inch (PPI) for a display.

    Args:
        width_px:   Horizontal resolution in pixels.
        height_px:  Vertical resolution in pixels.
        diagonal_in: Physical diagonal size of the screen in inches.

    Returns:
        PPI as a float, or raises ValueError for invalid inputs.
    """
    if width_px <= 0 or height_px <= 0:
        raise ValueError("Resolution must be positive.")
    if diagonal_in <= 0:
        raise ValueError("Screen diagonal must be positive.")
    pixel_diagonal = math.sqrt(width_px ** 2 + height_px ** 2)
    return pixel_diagonal / diagonal_in


def minimum_retina_ppi(distance_in: float) -> float:
    """Return the minimum PPI required for a Retina Display at *distance_in* inches.

    Uses Apple's criterion: individual pixels are indistinguishable when they
    subtend an angle smaller than one arcminute at the viewer's eye.

    Args:
        distance_in: Viewing distance in inches.

    Returns:
        Minimum PPI threshold as a float.
    """
    if distance_in <= 0:
        raise ValueError("Viewing distance must be positive.")
    return 1.0 / (distance_in * math.tan(_ARCMINUTE_RAD))


def is_retina(ppi: float, distance_in: float) -> bool:
    """Return True if *ppi* meets the Retina Display threshold at *distance_in* inches."""
    return ppi >= minimum_retina_ppi(distance_in)


class RetinaCheckerApp(tk.Tk):
    """Main application window for the Retina Display Checker."""

    _PADX = 12
    _PADY = 6

    def __init__(self):
        super().__init__()
        self.title("Retina Display Checker")
        self.resizable(False, False)

        # Detect screen resolution automatically.
        self._screen_w = self.winfo_screenwidth()
        self._screen_h = self.winfo_screenheight()

        self._build_ui()
        self._update_result()

    # ------------------------------------------------------------------
    # UI construction
    # ------------------------------------------------------------------

    def _build_ui(self):
        """Construct all widgets."""
        outer = ttk.Frame(self, padding=16)
        outer.grid(row=0, column=0, sticky="nsew")

        title_font = font.Font(family="Helvetica", size=16, weight="bold")
        ttk.Label(outer, text="Retina Display Checker", font=title_font).grid(
            row=0, column=0, columnspan=2, pady=(0, 14)
        )

        # ── Detected resolution ──────────────────────────────────────
        ttk.Label(outer, text="Detected resolution:").grid(
            row=1, column=0, sticky="w", padx=self._PADX, pady=self._PADY
        )
        self._resolution_var = tk.StringVar(
            value=f"{self._screen_w} × {self._screen_h} px"
        )
        ttk.Label(outer, textvariable=self._resolution_var, foreground="#333").grid(
            row=1, column=1, sticky="w", padx=self._PADX, pady=self._PADY
        )

        # ── Screen diagonal input ─────────────────────────────────────
        ttk.Label(outer, text="Screen diagonal (inches):").grid(
            row=2, column=0, sticky="w", padx=self._PADX, pady=self._PADY
        )
        self._diagonal_var = tk.StringVar(value="")
        diag_entry = ttk.Entry(outer, textvariable=self._diagonal_var, width=10)
        diag_entry.grid(row=2, column=1, sticky="w", padx=self._PADX, pady=self._PADY)
        diag_entry.bind("<KeyRelease>", lambda _e: self._update_result())

        # ── Pixel density (PPI) ───────────────────────────────────────
        ttk.Label(outer, text="Pixel density (PPI):").grid(
            row=3, column=0, sticky="w", padx=self._PADX, pady=self._PADY
        )
        self._ppi_var = tk.StringVar(value="—")
        ttk.Label(outer, textvariable=self._ppi_var, foreground="#333").grid(
            row=3, column=1, sticky="w", padx=self._PADX, pady=self._PADY
        )

        # ── Viewing distance dropdown ─────────────────────────────────
        ttk.Label(outer, text="Viewing distance:").grid(
            row=4, column=0, sticky="w", padx=self._PADX, pady=self._PADY
        )
        distance_labels = [label for label, _ in VIEWING_DISTANCES]
        self._distance_combo = ttk.Combobox(
            outer,
            values=distance_labels,
            state="readonly",
            width=30,
        )
        # Default to 18 in (laptop / small monitor)
        default_index = next(
            (i for i, (_, d) in enumerate(VIEWING_DISTANCES) if d == 18), 3
        )
        self._distance_combo.current(default_index)
        self._distance_combo.grid(
            row=4, column=1, sticky="w", padx=self._PADX, pady=self._PADY
        )
        self._distance_combo.bind("<<ComboboxSelected>>", lambda _e: self._update_result())

        # ── Required minimum PPI ──────────────────────────────────────
        ttk.Label(outer, text="Required min. PPI (retina):").grid(
            row=5, column=0, sticky="w", padx=self._PADX, pady=self._PADY
        )
        self._min_ppi_var = tk.StringVar(value="—")
        ttk.Label(outer, textvariable=self._min_ppi_var, foreground="#333").grid(
            row=5, column=1, sticky="w", padx=self._PADX, pady=self._PADY
        )

        # ── Separator ─────────────────────────────────────────────────
        ttk.Separator(outer, orient="horizontal").grid(
            row=6, column=0, columnspan=2, sticky="ew", pady=10
        )

        # ── Result label ──────────────────────────────────────────────
        result_font = font.Font(family="Helvetica", size=14, weight="bold")
        self._result_var = tk.StringVar(value="Enter screen diagonal to see result")
        self._result_label = ttk.Label(
            outer,
            textvariable=self._result_var,
            font=result_font,
            anchor="center",
        )
        self._result_label.grid(
            row=7, column=0, columnspan=2, pady=(6, 4), padx=self._PADX
        )

        # ── Explanation label ─────────────────────────────────────────
        self._explanation_var = tk.StringVar(value="")
        ttk.Label(
            outer,
            textvariable=self._explanation_var,
            wraplength=420,
            justify="center",
            foreground="#555",
        ).grid(row=8, column=0, columnspan=2, pady=(0, 6), padx=self._PADX)

    # ------------------------------------------------------------------
    # Logic / updates
    # ------------------------------------------------------------------

    def _selected_distance(self) -> float:
        """Return the currently selected viewing distance in inches."""
        idx = self._distance_combo.current()
        return VIEWING_DISTANCES[idx][1]

    def _update_result(self):
        """Recalculate PPI and retina status and refresh the UI."""
        distance_in = self._selected_distance()
        min_ppi = minimum_retina_ppi(distance_in)
        self._min_ppi_var.set(f"{min_ppi:.1f} PPI")

        raw = self._diagonal_var.get().strip()
        if not raw:
            self._ppi_var.set("—")
            self._result_var.set("Enter screen diagonal to see result")
            self._explanation_var.set("")
            self._result_label.configure(foreground="#555")
            return

        try:
            diagonal = float(raw)
            ppi = calculate_ppi(self._screen_w, self._screen_h, diagonal)
        except (ValueError, ZeroDivisionError):
            self._ppi_var.set("Invalid input")
            self._result_var.set("Please enter a valid diagonal size.")
            self._explanation_var.set("")
            self._result_label.configure(foreground="#cc0000")
            return

        self._ppi_var.set(f"{ppi:.1f} PPI")

        if is_retina(ppi, distance_in):
            self._result_var.set("✓  Retina Display")
            self._result_label.configure(foreground="#007a00")
            self._explanation_var.set(
                f"Your display's pixel density ({ppi:.1f} PPI) is above the "
                f"retina threshold ({min_ppi:.1f} PPI) for a viewing distance "
                f"of {distance_in} inches. Individual pixels are not "
                f"distinguishable to the human eye at this distance."
            )
        else:
            self._result_var.set("✗  Not a Retina Display")
            self._result_label.configure(foreground="#cc0000")
            self._explanation_var.set(
                f"Your display's pixel density ({ppi:.1f} PPI) is below the "
                f"retina threshold ({min_ppi:.1f} PPI) for a viewing distance "
                f"of {distance_in} inches. Individual pixels may be visible "
                f"to the human eye at this distance."
            )


def main():
    app = RetinaCheckerApp()
    app.mainloop()


if __name__ == "__main__":
    main()
