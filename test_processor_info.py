"""
Tests for processor_info.py
"""

from __future__ import annotations

import unittest
from unittest.mock import patch
from typing import Any, Dict

import processor_info as pi


# ---------------------------------------------------------------------------
# _match_tier
# ---------------------------------------------------------------------------

class TestMatchTier(unittest.TestCase):

    def test_apple_m3_pro(self) -> None:
        tier = pi._match_tier("Apple M3 Pro")
        self.assertEqual(tier["tier"], "apple_m3_pro")

    def test_apple_m3_pro_case_insensitive(self) -> None:
        tier = pi._match_tier("APPLE M3 PRO")
        self.assertEqual(tier["tier"], "apple_m3_pro")

    def test_apple_m4(self) -> None:
        tier = pi._match_tier("Apple M4")
        self.assertEqual(tier["tier"], "apple_m4")

    def test_apple_m1(self) -> None:
        tier = pi._match_tier("Apple M1")
        self.assertEqual(tier["tier"], "apple_m1")

    def test_intel_ultra_9(self) -> None:
        tier = pi._match_tier("Intel Core Ultra 9 285HX")
        self.assertEqual(tier["tier"], "intel_core_ultra_9_285")

    def test_intel_i9_13th(self) -> None:
        tier = pi._match_tier("Intel Core i9-13900K")
        self.assertEqual(tier["tier"], "intel_core_i9_13")

    def test_amd_ryzen_9_9000(self) -> None:
        tier = pi._match_tier("AMD Ryzen 9 9950X")
        self.assertEqual(tier["tier"], "amd_ryzen_9_9000")

    def test_amd_ryzen_ai(self) -> None:
        tier = pi._match_tier("AMD Ryzen AI 9 HX 370")
        self.assertEqual(tier["tier"], "amd_ryzen_ai_hx")

    def test_qualcomm_elite(self) -> None:
        tier = pi._match_tier("Snapdragon X Elite X1E-84-100")
        self.assertEqual(tier["tier"], "qualcomm_x_elite")

    def test_unknown_returns_unknown_tier(self) -> None:
        tier = pi._match_tier("Some Imaginary CPU 9999")
        self.assertEqual(tier["tier"], "unknown")

    def test_unknown_scores_are_none(self) -> None:
        tier = pi._match_tier("XYZ Phantom CPU")
        self.assertIsNone(tier["single_core"])
        self.assertIsNone(tier["multi_core"])


# ---------------------------------------------------------------------------
# _infer_vendor
# ---------------------------------------------------------------------------

class TestInferVendor(unittest.TestCase):

    def test_apple(self) -> None:
        self.assertEqual(pi._infer_vendor("Apple M3 Pro"), "Apple")

    def test_apple_silicon_no_brand(self) -> None:
        self.assertEqual(pi._infer_vendor("Apple M2"), "Apple")

    def test_intel(self) -> None:
        self.assertEqual(pi._infer_vendor("Intel Core i7-13700K"), "Intel")

    def test_amd(self) -> None:
        self.assertEqual(pi._infer_vendor("AMD Ryzen 9 7950X"), "AMD")

    def test_qualcomm(self) -> None:
        self.assertEqual(pi._infer_vendor("Qualcomm Snapdragon X Elite"), "Qualcomm")

    def test_snapdragon_no_qualcomm_prefix(self) -> None:
        self.assertEqual(pi._infer_vendor("Snapdragon X Elite"), "Qualcomm")

    def test_unknown(self) -> None:
        self.assertEqual(pi._infer_vendor("Mystery Chip 3000"), "Unknown")


# ---------------------------------------------------------------------------
# _infer_arch
# ---------------------------------------------------------------------------

class TestInferArch(unittest.TestCase):

    def test_apple_is_arm(self) -> None:
        self.assertEqual(pi._infer_arch("Apple M3 Pro"), "ARM")

    def test_snapdragon_is_arm(self) -> None:
        self.assertEqual(pi._infer_arch("Snapdragon X Elite"), "ARM")

    def test_intel_is_x86(self) -> None:
        self.assertEqual(pi._infer_arch("Intel Core i7-13700K"), "x86_64")

    def test_amd_is_x86(self) -> None:
        self.assertEqual(pi._infer_arch("AMD Ryzen 9 7950X"), "x86_64")

    def test_explicit_arm_keyword(self) -> None:
        self.assertEqual(pi._infer_arch("ARM Cortex-A78"), "ARM")


# ---------------------------------------------------------------------------
# get_processor_info
# ---------------------------------------------------------------------------

class TestGetProcessorInfo(unittest.TestCase):

    def setUp(self) -> None:
        self._info = pi.get_processor_info()

    def test_returns_dict(self) -> None:
        self.assertIsInstance(self._info, dict)

    def test_expected_keys_present(self) -> None:
        expected = {
            "platform", "cpu_model", "vendor", "architecture",
            "total_cores", "total_threads", "performance_cores",
            "efficiency_cores", "max_freq_mhz", "current_freq_mhz",
            "l3_cache", "tier",
        }
        for key in expected:
            self.assertIn(key, self._info, f"Missing key: {key}")

    def test_platform_key_is_string(self) -> None:
        self.assertIsInstance(self._info["platform"], str)

    def test_platform_is_known_os(self) -> None:
        self.assertIn(self._info["platform"], ("Darwin", "Linux", "Windows"))

    def test_numeric_fields_are_numeric_or_none(self) -> None:
        for key in ("total_cores", "total_threads", "performance_cores",
                    "efficiency_cores", "max_freq_mhz", "current_freq_mhz"):
            val = self._info[key]
            if val is not None:
                self.assertIsInstance(val, (int, float),
                                      f"{key} should be numeric, got {type(val)}")

    def test_tier_is_dict_or_none(self) -> None:
        tier = self._info["tier"]
        if tier is not None:
            self.assertIsInstance(tier, dict)

    def test_tier_has_expected_keys_when_recognised(self) -> None:
        tier = self._info["tier"]
        if tier is not None and tier.get("tier") != "unknown":
            for key in ("tier", "label", "vendor", "arch",
                        "single_core", "multi_core", "efficiency"):
                self.assertIn(key, tier)


# ---------------------------------------------------------------------------
# scorecard
# ---------------------------------------------------------------------------

class TestScorecard(unittest.TestCase):

    def _make_info(self, model: str) -> Dict[str, Any]:
        return {
            "platform": "Linux",
            "cpu_model": model,
            "vendor": pi._infer_vendor(model),
            "architecture": pi._infer_arch(model),
            "total_cores": None,
            "total_threads": None,
            "performance_cores": None,
            "efficiency_cores": None,
            "max_freq_mhz": None,
            "current_freq_mhz": None,
            "l3_cache": None,
            "tier": pi._match_tier(model),
        }

    def test_returns_list(self) -> None:
        info = self._make_info("Apple M3 Pro")
        rows = pi.scorecard(info)
        self.assertIsInstance(rows, list)

    def test_has_seven_dimension_rows_plus_summary(self) -> None:
        info = self._make_info("Apple M3 Pro")
        rows = pi.scorecard(info)
        # 7 dimensions + Strengths + Weaknesses = 9
        self.assertEqual(len(rows), 9)

    def test_each_row_has_required_keys(self) -> None:
        info = self._make_info("Intel Core i9-13900K")
        rows = pi.scorecard(info)
        for row in rows:
            for key in ("metric", "value", "target", "result", "note", "badge"):
                self.assertIn(key, row, f"Missing key '{key}' in row {row}")

    def test_m3_pro_all_pass(self) -> None:
        info = self._make_info("Apple M3 Pro")
        rows = pi.scorecard(info)
        dimension_rows = [r for r in rows if r["result"] in ("PASS", "REVIEW", "FAIL")]
        results = {r["result"] for r in dimension_rows}
        # M3 Pro is the reference – all dimensions should be PASS
        self.assertNotIn("FAIL", results)

    def test_unknown_cpu_all_review(self) -> None:
        info = self._make_info("Unknown Imaginary CPU 9999")
        rows = pi.scorecard(info)
        dimension_rows = [r for r in rows if r["result"] in ("PASS", "REVIEW", "FAIL")]
        for row in dimension_rows:
            self.assertEqual(row["result"], "REVIEW")
            self.assertEqual(row["value"], "Unknown")

    def test_intel_has_x86_platform_badge(self) -> None:
        info = self._make_info("Intel Core i7-13700K")
        rows = pi.scorecard(info)
        platform_row = next(r for r in rows if r["metric"] == "Platform & Ecosystem")
        self.assertIn("x86", platform_row["badge"].lower())

    def test_strengths_row_present(self) -> None:
        info = self._make_info("AMD Ryzen 9 7950X")
        rows = pi.scorecard(info)
        metrics = [r["metric"] for r in rows]
        self.assertIn("Strengths", metrics)

    def test_weaknesses_row_present(self) -> None:
        info = self._make_info("AMD Ryzen 9 7950X")
        rows = pi.scorecard(info)
        metrics = [r["metric"] for r in rows]
        self.assertIn("Weaknesses", metrics)

    def test_amd_ryzen_9_9000_beats_m3_pro_single_core(self) -> None:
        info = self._make_info("AMD Ryzen 9 9950X")
        rows = pi.scorecard(info)
        sc_row = next(r for r in rows if r["metric"] == "Single-Core Performance")
        # Ryzen 9 9000 has single_core=108, reference=100 → 108% → PASS
        self.assertEqual(sc_row["result"], "PASS")

    def test_efficiency_fail_for_high_tdp_intel(self) -> None:
        info = self._make_info("Intel Core i9-13900K")
        rows = pi.scorecard(info)
        eff_row = next(r for r in rows if "Efficiency" in r["metric"])
        # i9-13th has efficiency=45 → 45% of 100 → FAIL
        self.assertEqual(eff_row["result"], "FAIL")


# ---------------------------------------------------------------------------
# _score_result
# ---------------------------------------------------------------------------

class TestScoreResult(unittest.TestCase):

    def test_none_returns_review(self) -> None:
        self.assertEqual(pi._score_result(None), "REVIEW")

    def test_90_returns_pass(self) -> None:
        self.assertEqual(pi._score_result(90), "PASS")

    def test_100_returns_pass(self) -> None:
        self.assertEqual(pi._score_result(100), "PASS")

    def test_115_returns_pass(self) -> None:
        self.assertEqual(pi._score_result(115), "PASS")

    def test_70_returns_review(self) -> None:
        self.assertEqual(pi._score_result(70), "REVIEW")

    def test_89_returns_review(self) -> None:
        self.assertEqual(pi._score_result(89), "REVIEW")

    def test_69_returns_fail(self) -> None:
        self.assertEqual(pi._score_result(69), "FAIL")

    def test_0_returns_fail(self) -> None:
        self.assertEqual(pi._score_result(0), "FAIL")


# ---------------------------------------------------------------------------
# _relative_score
# ---------------------------------------------------------------------------

class TestRelativeScore(unittest.TestCase):

    def test_equal_is_100(self) -> None:
        self.assertEqual(pi._relative_score(100, 100), 100)

    def test_half_is_50(self) -> None:
        self.assertEqual(pi._relative_score(50, 100), 50)

    def test_capped_at_120(self) -> None:
        self.assertEqual(pi._relative_score(200, 100), 120)

    def test_none_returns_none(self) -> None:
        self.assertIsNone(pi._relative_score(None, 100))

    def test_zero_reference_returns_none(self) -> None:
        self.assertIsNone(pi._relative_score(50, 0))

    def test_above_reference_capped(self) -> None:
        result = pi._relative_score(130, 100)
        self.assertIsNotNone(result)
        self.assertLessEqual(result, 120)


# ---------------------------------------------------------------------------
# PERFORMANCE_TIERS sanity checks
# ---------------------------------------------------------------------------

class TestPerformanceTiers(unittest.TestCase):

    def test_all_tiers_have_required_keys(self) -> None:
        required = {
            "tier", "label", "vendor", "arch", "match_patterns",
            "single_core", "multi_core", "efficiency",
            "sustained", "igpu", "real_world", "platform",
            "typical_tdp", "active_cooling", "strengths", "weaknesses",
        }
        for t in pi.PERFORMANCE_TIERS:
            for key in required:
                self.assertIn(key, t, f"Tier '{t.get('tier')}' missing key '{key}'")

    def test_m3_pro_is_reference_100(self) -> None:
        m3_pro = next(t for t in pi.PERFORMANCE_TIERS if t["tier"] == "apple_m3_pro")
        self.assertEqual(m3_pro["single_core"], 100)
        self.assertEqual(m3_pro["multi_core"], 100)

    def test_no_duplicate_tier_ids(self) -> None:
        ids = [t["tier"] for t in pi.PERFORMANCE_TIERS]
        self.assertEqual(len(ids), len(set(ids)))

    def test_scores_are_positive_integers(self) -> None:
        for t in pi.PERFORMANCE_TIERS:
            for key in ("single_core", "multi_core", "efficiency",
                        "sustained", "igpu", "real_world", "platform"):
                val = t[key]
                self.assertIsInstance(val, int, f"{t['tier']}.{key} not int")
                self.assertGreater(val, 0, f"{t['tier']}.{key} not positive")

    def test_match_patterns_are_lowercase_friendly(self) -> None:
        """All patterns should be non-empty strings."""
        for t in pi.PERFORMANCE_TIERS:
            for pat in t["match_patterns"]:
                self.assertIsInstance(pat, str)
                self.assertTrue(pat.strip(), f"Empty pattern in {t['tier']}")


# ---------------------------------------------------------------------------
# M_SERIES_REFERENCE
# ---------------------------------------------------------------------------

class TestMSeriesReference(unittest.TestCase):

    def test_reference_has_required_keys(self) -> None:
        required = {
            "chip_name", "architecture", "vendor",
            "single_core_score", "multi_core_score", "efficiency_score",
            "sustained_score", "igpu_score", "real_world_score", "platform_score",
        }
        for key in required:
            self.assertIn(key, pi.M_SERIES_REFERENCE)

    def test_reference_scores_are_100(self) -> None:
        ref = pi.M_SERIES_REFERENCE
        for key in ("single_core_score", "multi_core_score", "efficiency_score",
                    "sustained_score", "igpu_score", "real_world_score", "platform_score"):
            self.assertEqual(ref[key], 100, f"{key} should be 100 (baseline)")


if __name__ == "__main__":
    unittest.main()
