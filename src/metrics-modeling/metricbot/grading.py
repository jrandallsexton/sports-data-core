"""Grade a week's predictions against final scores.

Pure pandas/numpy — no I/O — so every rule here is unit-testable offline.
The rules follow the harness spec in
docs/metrics-modeling/matchup-preview-data-inputs.md §6:

- Accuracy is only meaningful as an edge over a baseline, so both SU
  baselines (always-home, favorite-by-spread) ride along with the raw
  number, and ATS carries the 52.4% break-even reference.
- Pushes and ties are EXCLUDED from accuracy denominators and COUNTED
  in the report — silently folding them either way biases the number.
- Calibration is the point (the stated goal is "genuinely interesting
  information", not beating Vegas): Brier scores plus decile buckets of
  predicted-vs-realized frequency.
- O/U is deliberately absent: the model predicts margin, not totals.
"""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd

# ATS break-even at standard -110 juice; a fixed reference, not a baseline
# computed from the data.
ATS_BREAK_EVEN = 0.5238


@dataclass
class GradeReport:
    week_contests: int          # predictions made
    graded: int                 # had a final score to grade against
    ungradeable: int            # no final score (cancelled late, not final)

    su: dict = field(default_factory=dict)
    ats: dict = field(default_factory=dict)
    margin: dict = field(default_factory=dict)
    calibration: list = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "week_contests": self.week_contests,
            "graded": self.graded,
            "ungradeable": self.ungradeable,
            "su": self.su,
            "ats": self.ats,
            "margin": self.margin,
            "calibration": self.calibration,
        }


def _ratio(numerator: int, denominator: int) -> float | None:
    return round(numerator / denominator, 4) if denominator else None


def grade_week(predictions: pd.DataFrame, scores: pd.DataFrame) -> GradeReport:
    """predictions: the pipeline's per-contest frame (ContestId,
    PredictedMargin, WinProbability, HomeCoverProbability, Spread).
    scores: ContestId, HomeScore, AwayScore for the games that finished.
    """
    merged = predictions.merge(
        scores, on="ContestId", how="left", suffixes=("", "Final"),
        validate="one_to_one")

    # The as-of slate emits literal NULL score columns; the merge brings
    # the real ones in under *Final when the names collide.
    home_col = "HomeScoreFinal" if "HomeScoreFinal" in merged.columns else "HomeScore"
    away_col = "AwayScoreFinal" if "AwayScoreFinal" in merged.columns else "AwayScore"

    graded = merged[merged[home_col].notna() & merged[away_col].notna()].copy()
    graded["ActualMargin"] = graded[home_col].astype(float) - graded[away_col].astype(float)

    report = GradeReport(
        week_contests=len(merged),
        graded=len(graded),
        ungradeable=len(merged) - len(graded),
    )
    if len(graded) == 0:
        return report

    # ── Straight-up ──────────────────────────────────────────────────
    decided = graded[graded["ActualMargin"] != 0].copy()
    ties = len(graded) - len(decided)

    if len(decided) == 0:
        # Every graded game tied: means over empty frames are nan, and a
        # nan Brier in the report reads as a number. Say "no data" instead.
        report.su = {
            "decided": 0,
            "ties_excluded": ties,
            "correct": 0,
            "accuracy": None,
            "baseline_always_home": None,
            "baseline_favorite": {"games_with_spread": 0, "accuracy": None},
            "brier": None,
            "brier_climatology": None,
        }
    else:
        actual_home_win = decided["ActualMargin"] > 0
        picked_home = decided["WinProbability"] >= 0.5
        su_correct = int((picked_home == actual_home_win).sum())

        # Favorite-by-spread baseline: spread < 0 = home favored. Pick-em
        # (0) and missing spreads drop out of this baseline's denominator.
        with_spread = decided[decided["Spread"].notna() & (decided["Spread"] != 0)]
        favorite_correct = int(
            ((with_spread["Spread"] < 0) == (with_spread["ActualMargin"] > 0)).sum())

        su_brier = float(np.mean(
            (decided["WinProbability"] - actual_home_win.astype(float)) ** 2))
        home_rate = float(actual_home_win.mean())
        climatology_brier = float(np.mean(
            (home_rate - actual_home_win.astype(float)) ** 2))

        report.su = {
            "decided": len(decided),
            "ties_excluded": ties,
            "correct": su_correct,
            "accuracy": _ratio(su_correct, len(decided)),
            "baseline_always_home": round(home_rate, 4),
            "baseline_favorite": {
                "games_with_spread": len(with_spread),
                "accuracy": _ratio(favorite_correct, len(with_spread)),
            },
            "brier": round(su_brier, 4),
            "brier_climatology": round(climatology_brier, 4),
        }

    # ── Against the spread ───────────────────────────────────────────
    spread_games = graded[graded["Spread"].notna()].copy()
    no_spread = len(graded) - len(spread_games)

    spread_games["CoverMargin"] = spread_games["ActualMargin"] + spread_games["Spread"]
    pushes = int((spread_games["CoverMargin"] == 0).sum())
    ats_decided = spread_games[spread_games["CoverMargin"] != 0]

    actual_home_cover = ats_decided["CoverMargin"] > 0
    picked_home_cover = ats_decided["HomeCoverProbability"] >= 0.5
    ats_correct = int((picked_home_cover == actual_home_cover).sum())

    ats_brier = float(np.mean(
        (ats_decided["HomeCoverProbability"] - actual_home_cover.astype(float)) ** 2)) \
        if len(ats_decided) else None

    report.ats = {
        "decided": len(ats_decided),
        "pushes_excluded": pushes,
        "no_spread_ungraded": no_spread,
        "correct": ats_correct,
        "accuracy": _ratio(ats_correct, len(ats_decided)),
        "break_even_reference": ATS_BREAK_EVEN,
        "brier": round(ats_brier, 4) if ats_brier is not None else None,
    }

    # ── Margin: the model vs the market on identical games ───────────
    model_errors = (graded["PredictedMargin"] - graded["ActualMargin"]).abs()
    market = spread_games.copy()
    market_errors = (-market["Spread"] - market["ActualMargin"]).abs()

    report.margin = {
        "model_mae": round(float(model_errors.mean()), 2),
        "model_rmse": round(float(np.sqrt((model_errors ** 2).mean())), 2),
        # The spread AS a margin predictor, on the games that have one —
        # the honest head-to-head with the market.
        "market_mae": round(float(market_errors.mean()), 2) if len(market) else None,
        "market_rmse": round(float(np.sqrt((market_errors ** 2).mean())), 2) if len(market) else None,
        "market_games": len(market),
    }

    # ── Calibration deciles over P(home wins) ────────────────────────
    buckets = pd.cut(decided["WinProbability"], bins=np.arange(0.0, 1.05, 0.1),
                     include_lowest=True)
    for interval, group in decided.groupby(buckets, observed=True):
        if len(group) == 0:
            continue
        report.calibration.append({
            "bucket": f"{interval.left:.1f}-{interval.right:.1f}",
            "games": len(group),
            "predicted": round(float(group["WinProbability"].mean()), 4),
            "actual": round(float((group["ActualMargin"] > 0).mean()), 4),
        })

    return report


def format_report(report: GradeReport, header: str) -> str:
    """Human-readable rendering for the CLI; the service returns to_dict()."""
    r = report
    lines = [
        header,
        f"  Contests: {r.week_contests} predicted, {r.graded} graded, "
        f"{r.ungradeable} ungradeable",
    ]
    if r.su:
        su = r.su
        fav = su["baseline_favorite"]
        if su["accuracy"] is None:
            lines += [
                "",
                f"  STRAIGHT UP   n/a — no decided games"
                + (f" ({su['ties_excluded']} ties excluded)" if su["ties_excluded"] else ""),
            ]
        else:
            lines += [
                "",
                f"  STRAIGHT UP   {su['correct']}/{su['decided']}  ({su['accuracy']:.1%})",
                f"    vs always-home {su['baseline_always_home']:.1%}"
                + (f"  |  vs favorite {fav['accuracy']:.1%} ({fav['games_with_spread']} spread games)"
                   if fav["accuracy"] is not None else ""),
                f"    Brier {su['brier']:.4f} (climatology {su['brier_climatology']:.4f}; lower is better)"
                + (f"  |  {su['ties_excluded']} ties excluded" if su["ties_excluded"] else ""),
            ]
    if r.ats:
        ats = r.ats
        if ats["accuracy"] is None:
            lines += ["", "  ATS           n/a — no decided spread games"]
        else:
            lines += [
                "",
                f"  ATS           {ats['correct']}/{ats['decided']}  ({ats['accuracy']:.1%})",
                f"    break-even {ATS_BREAK_EVEN:.1%}  |  {ats['pushes_excluded']} pushes excluded"
                + (f"  |  {ats['no_spread_ungraded']} without a spread" if ats["no_spread_ungraded"] else "")
                + (f"  |  Brier {ats['brier']:.4f}" if ats["brier"] is not None else ""),
            ]
    if r.margin:
        m = r.margin
        market = (f"  |  market (spread) MAE {m['market_mae']:.2f} on {m['market_games']}"
                  if m["market_mae"] is not None else "")
        lines += ["", f"  MARGIN        model MAE {m['model_mae']:.2f}, RMSE {m['model_rmse']:.2f}{market}"]
    if r.calibration:
        lines += ["", "  CALIBRATION (P(home win) decile: predicted -> actual, n)"]
        for b in r.calibration:
            lines.append(
                f"    {b['bucket']}:  {b['predicted']:.2f} -> {b['actual']:.2f}   (n={b['games']})")
    return "\n".join(lines)
