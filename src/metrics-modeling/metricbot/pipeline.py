"""The run-week orchestration: extract → train → predict SU → predict ATS
→ build DTOs → POST. State stays in memory on the happy path;
--dump-intermediate writes the same CSV/JSON artifacts the prototype
produced, for debugging parity.
"""

from __future__ import annotations

import json
import logging
import re
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import pandas as pd

from . import MODEL_VERSION
from .api import post_predictions
from .config import Config, normalize_sport
from .dtos import build_prediction_dtos
from .extract import (detect_current_season_week, extract_asof_training,
                      extract_asof_week, extract_current_week,
                      extract_final_scores, extract_training)
from .grading import GradeReport, format_report, grade_week
from .model import predict_market_prior

logger = logging.getLogger("metricbot")

DATA_DIR = Path(__file__).resolve().parent.parent / "data"


@dataclass
class RunResult:
    """Structured outcome — the HTTP service returns this; the CLI ignores it."""
    season_year: int | None
    week: int
    training_rows: int
    contests: int
    mae: float
    residual_std: float
    published: bool
    elapsed_seconds: float
    dtos: list[dict]


def run_week(sport: str,
             dry_run: bool,
             dump_intermediate: bool,
             season_year: int | None = None,
             week: int | None = None,
             prior_tail: int = 0,
             publish: bool = False,
             legacy_extraction: bool = False,
             return_result: bool = False) -> int | RunResult:
    started = datetime.now(timezone.utc)
    sport = normalize_sport(sport)
    config = Config.load(sport)

    explicit_week = season_year is not None and week is not None
    if (season_year is None) != (week is None):
        raise SystemExit("--season-year and --week must be provided together.")

    # Explicit-week runs are EXPERIMENTS: they never publish unless
    # --publish is explicit — a backtest must not overwrite live
    # deetsMeter predictions. Live (auto-detected) runs publish normally.
    effective_dry_run = dry_run or (explicit_week and not publish)

    # Live runs resolve NOW -> (season, week) and use the SAME as-of
    # extraction as experiments: entering-week features (identical to the
    # live aggregates mid-season, and the only thing that works in weeks
    # 1-2 when FranchiseSeasonMetric is empty), leak-free training, and
    # prior-season tail support. The legacy extraction remains available
    # behind --legacy-extraction for parity comparison only.
    if not explicit_week and not legacy_extraction:
        season_year, week = detect_current_season_week(config)

    mode = ("legacy-live" if legacy_extraction
            else f"{'as-of' if explicit_week else 'live'} {season_year} wk{week} (tail={prior_tail})")
    logger.info("MetricBot %s run-week starting. Sport: %s, Database: %s, Mode: %s, DryRun: %s",
                MODEL_VERSION, sport, config.pg_database, mode, effective_dry_run)

    # ── Extract ──────────────────────────────────────────────────────
    if legacy_extraction:
        from .extract import detect_week_number
        training = extract_training(config)
        current_week = extract_current_week(config)
        week_number = detect_week_number(current_week)
    else:
        training = extract_asof_training(config, season_year, week)
        current_week = extract_asof_week(config, season_year, week, prior_tail)
        week_number = week
    logger.info("Extracted %d training rows; %d contests for week %d",
                len(training), len(current_week), week_number)

    # ── Model (v1.1: market-prior residual + pure-stats fallback) ────
    result = predict_market_prior(training, current_week, config.fbs_scope)
    predictions = result.predictions
    logger.info(
        "v1.1 model: %d pure-corpus games (MAE %.2f, std %.2f); residual corpus %d "
        "(correction std %s); slate paths: %s",
        result.training_rows, result.mae, result.residual_std, result.residual_rows,
        f"{result.residual_model_std:.2f}" if result.residual_model_std else "n/a — fallback-only",
        predictions["ModelPath"].value_counts().to_dict())

    # ── DTOs ─────────────────────────────────────────────────────────
    dtos = build_prediction_dtos(predictions)
    logger.info("Built %d prediction DTOs (%d SU + %d ATS — ATS for priced games only)",
                len(dtos), len(predictions), len(dtos) - len(predictions))

    if dump_intermediate:
        label = (f"{sport}_legacy_week_{week_number}" if legacy_extraction
                 else f"{sport}_{'asof' if explicit_week else 'live'}_{season_year}_wk{week_number}_tail{prior_tail}")
        _dump(training, current_week, predictions, dtos, label)

    # ── Publish ──────────────────────────────────────────────────────
    if effective_dry_run:
        reason = "DRY RUN" if dry_run else "EXPLICIT-WEEK RUN (pass --publish to override)"
        logger.info("%s — skipping POST. %d DTOs ready for %s/admin/ai-predictions/%s",
                    reason, len(dtos), config.api_base_url, config.metricbot_user_id)
    else:
        response = post_predictions(config, dtos)
        logger.info("Published predictions. API response: %s", response)

    elapsed = (datetime.now(timezone.utc) - started).total_seconds()
    logger.info("MetricBot run-week complete in %.1fs. Sport: %s, Week: %d, Contests: %d",
                elapsed, sport, week_number, len(predictions))

    if return_result:
        return RunResult(
            season_year=season_year,
            week=week_number,
            training_rows=result.training_rows,
            contests=len(predictions),
            mae=result.mae,
            residual_std=result.residual_std,
            published=not effective_dry_run,
            elapsed_seconds=elapsed,
            dtos=dtos,
        )
    return 0


def run_backtest(sport: str,
                 season_year: int,
                 week: int,
                 prior_tail: int = 0,
                 dump_intermediate: bool = False) -> "BacktestResult":
    """Predict a historical week as-of, then grade against final scores.

    Never publishes — a backtest exists to be scored, not to overwrite
    live deetsMeter data. The prediction path is EXACTLY run_week's
    as-of mode (same extraction, same model); grading is a second
    extraction (final scores) plus pure-pandas scoring.
    """
    started = datetime.now(timezone.utc)
    sport = normalize_sport(sport)
    config = Config.load(sport)

    logger.info("MetricBot %s backtest starting. Sport: %s, %d wk%d (tail=%d)",
                MODEL_VERSION, sport, season_year, week, prior_tail)

    training = extract_asof_training(config, season_year, week)
    current_week = extract_asof_week(config, season_year, week, prior_tail)
    logger.info("Extracted %d training rows; %d contests for week %d",
                len(training), len(current_week), week)

    result = predict_market_prior(training, current_week, config.fbs_scope)
    predictions = result.predictions

    scores = extract_final_scores(config, season_year, week)
    report = grade_week(predictions, scores)

    if dump_intermediate:
        label = f"{sport}_backtest_{season_year}_wk{week}_tail{prior_tail}"
        _dump(training, current_week, predictions,
              build_prediction_dtos(predictions), label)

    elapsed = (datetime.now(timezone.utc) - started).total_seconds()
    logger.info("Backtest complete in %.1fs. SU %s, ATS %s, model MAE %s vs market %s",
                elapsed, report.su.get("accuracy"), report.ats.get("accuracy"),
                report.margin.get("model_mae"), report.margin.get("market_mae"))

    return BacktestResult(
        sport=sport,
        season_year=season_year,
        week=week,
        prior_season_tail=prior_tail,
        training_rows=result.training_rows,
        in_sample_mae=result.mae,
        residual_std=result.residual_std,
        report=report,
        elapsed_seconds=elapsed,
    )


@dataclass
class BacktestResult:
    sport: str
    season_year: int
    week: int
    prior_season_tail: int
    training_rows: int
    in_sample_mae: float
    residual_std: float
    report: GradeReport
    elapsed_seconds: float

    def format(self) -> str:
        header = (f"BACKTEST {self.sport} {self.season_year} week {self.week} "
                  f"(tail={self.prior_season_tail}) — {MODEL_VERSION}")
        return format_report(self.report, header)


def _dump(training: pd.DataFrame,
          current_week: pd.DataFrame,
          predictions: pd.DataFrame,
          dtos: list[dict],
          stamp: str) -> None:
    """Write the prototype-equivalent artifacts for debugging."""
    # Stamp is built from CLI/HTTP inputs — allow only filename-safe
    # characters so it can never escape DATA_DIR (CodeQL: path injection).
    stamp = re.sub(r"[^A-Za-z0-9_.-]", "_", stamp)[:120]

    DATA_DIR.mkdir(exist_ok=True)

    training.to_csv(DATA_DIR / f"training_{stamp}.csv", index=False)
    current_week.to_csv(DATA_DIR / f"current_{stamp}.csv", index=False)
    predictions.to_csv(DATA_DIR / f"predictions_{stamp}.csv", index=False)
    (DATA_DIR / f"contest_predictions_{stamp}.json").write_text(
        json.dumps(dtos, indent=2), encoding="utf-8")

    logger.info("Intermediate artifacts written to %s (*_%s.*)", DATA_DIR, stamp)
