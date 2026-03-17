"""Unit tests for the core logic in retina_checker.py."""

import math
import unittest

from retina_checker import (
    VIEWING_DISTANCES,
    calculate_ppi,
    is_retina,
    minimum_retina_ppi,
)


class TestCalculatePPI(unittest.TestCase):
    """Tests for calculate_ppi()."""

    def test_known_iphone_resolution(self):
        """iPhone 13 (2532×1170, 6.06-inch diagonal) is nominally 460 PPI."""
        ppi = calculate_ppi(2532, 1170, 6.06)
        self.assertAlmostEqual(ppi, 460, delta=5)

    def test_known_macbook_pro_14(self):
        """MacBook Pro 14-inch (3024×1964, 14.2-inch) is ~254 PPI."""
        ppi = calculate_ppi(3024, 1964, 14.2)
        self.assertAlmostEqual(ppi, 254, delta=5)

    def test_1080p_27_inch_monitor(self):
        """1920×1080 on a 27-inch monitor is ~81.6 PPI."""
        ppi = calculate_ppi(1920, 1080, 27.0)
        self.assertAlmostEqual(ppi, 81.6, delta=1)

    def test_zero_diagonal_raises(self):
        with self.assertRaises(ValueError):
            calculate_ppi(1920, 1080, 0)

    def test_negative_diagonal_raises(self):
        with self.assertRaises(ValueError):
            calculate_ppi(1920, 1080, -5)

    def test_zero_width_raises(self):
        with self.assertRaises(ValueError):
            calculate_ppi(0, 1080, 24)

    def test_zero_height_raises(self):
        with self.assertRaises(ValueError):
            calculate_ppi(1920, 0, 24)

    def test_formula_correctness(self):
        """PPI = sqrt(w²+h²) / diagonal."""
        w, h, d = 1920, 1080, 24.0
        expected = math.sqrt(w ** 2 + h ** 2) / d
        self.assertAlmostEqual(calculate_ppi(w, h, d), expected)


class TestMinimumRetinaPPI(unittest.TestCase):
    """Tests for minimum_retina_ppi()."""

    def test_10_inch_distance(self):
        """At 10 inches the threshold should be ~343.8 PPI."""
        threshold = minimum_retina_ppi(10)
        self.assertAlmostEqual(threshold, 343.8, delta=1)

    def test_18_inch_distance(self):
        """At 18 inches the threshold should be ~191 PPI."""
        threshold = minimum_retina_ppi(18)
        self.assertAlmostEqual(threshold, 191, delta=2)

    def test_24_inch_distance(self):
        """At 24 inches the threshold should be ~143 PPI."""
        threshold = minimum_retina_ppi(24)
        self.assertAlmostEqual(threshold, 143, delta=2)

    def test_threshold_decreases_with_distance(self):
        """Farther distance → lower minimum PPI required."""
        for d1, d2 in zip(range(10, 30), range(11, 31)):
            self.assertGreater(minimum_retina_ppi(d1), minimum_retina_ppi(d2))

    def test_zero_distance_raises(self):
        with self.assertRaises(ValueError):
            minimum_retina_ppi(0)

    def test_negative_distance_raises(self):
        with self.assertRaises(ValueError):
            minimum_retina_ppi(-1)


class TestIsRetina(unittest.TestCase):
    """Tests for is_retina()."""

    def test_iphone_at_10_inches_is_retina(self):
        """iPhone (~460 PPI) at 10 inches should be retina."""
        self.assertTrue(is_retina(460, 10))

    def test_low_ppi_monitor_at_close_distance_not_retina(self):
        """81 PPI monitor at 10 inches is not retina."""
        self.assertFalse(is_retina(81, 10))

    def test_1080p_27in_monitor_at_24in_not_retina(self):
        """81.6 PPI at 24 inches is below the ~143 PPI threshold."""
        ppi = calculate_ppi(1920, 1080, 27.0)
        self.assertFalse(is_retina(ppi, 24))

    def test_4k_27in_monitor_at_24in_is_retina(self):
        """4K (3840×2160) on 27-inch is ~163 PPI; retina at 24 inches."""
        ppi = calculate_ppi(3840, 2160, 27.0)
        self.assertTrue(is_retina(ppi, 24))

    def test_exactly_at_threshold(self):
        """A display exactly at the threshold is considered retina."""
        distance = 20
        threshold_ppi = minimum_retina_ppi(distance)
        self.assertTrue(is_retina(threshold_ppi, distance))

    def test_just_below_threshold_not_retina(self):
        distance = 20
        threshold_ppi = minimum_retina_ppi(distance)
        self.assertFalse(is_retina(threshold_ppi - 0.001, distance))


class TestViewingDistances(unittest.TestCase):
    """Tests that VIEWING_DISTANCES is well-formed."""

    def test_all_entries_have_positive_distances(self):
        for label, dist in VIEWING_DISTANCES:
            self.assertGreater(dist, 0, f"Distance for '{label}' must be positive.")

    def test_labels_are_strings(self):
        for label, _ in VIEWING_DISTANCES:
            self.assertIsInstance(label, str)

    def test_at_least_one_entry(self):
        self.assertGreater(len(VIEWING_DISTANCES), 0)


if __name__ == "__main__":
    unittest.main()
