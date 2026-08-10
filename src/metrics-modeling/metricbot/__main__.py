"""CLI entry point: python -m metricbot run-week [options]"""

from __future__ import annotations

import argparse
import logging
import sys

from .config import normalize_sport
from .pipeline import run_backtest, run_week


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="metricbot",
        description="MetricBot prediction pipeline (deetsMeter). One command replaces "
                    "Generate-Predictions.ps1 + CSV shuffling + the Postman POST.")
    subparsers = parser.add_subparsers(dest="command", required=True)

    run = subparsers.add_parser(
        "run-week",
        help="Extract current-week slate, train, predict SU + ATS, publish to the API.")
    run.add_argument(
        "--sport", type=normalize_sport, default="FootballNcaa",
        metavar="{FootballNcaa,FootballNfl}",
        help="Platform Sport enum name (case-insensitive). "
             "Default: FootballNcaa.")
    run.add_argument(
        "--dry-run", action="store_true",
        help="Do everything except the POST — inspect DTO counts/logs first.")
    run.add_argument(
        "--dump-intermediate", action="store_true",
        help="Write prototype-equivalent CSV/JSON artifacts to ./data for debugging.")
    run.add_argument(
        "--season-year", type=int, default=None,
        help="AS-OF mode (with --week): backtest a historical week using only "
             "data available entering it. As-of runs never POST unless --publish.")
    run.add_argument(
        "--week", type=int, default=None,
        help="AS-OF mode (with --season-year): the week whose slate to predict.")
    run.add_argument(
        "--prior-season-tail", type=int, default=0, metavar="N",
        help="Top up thin early-week feature windows with the team's most recent "
             "N prior-season (regular/post) games. Works in live AND as-of runs "
             "(essential for weeks 1-2 when no current-season games exist). "
             "Default 0 = off.")
    run.add_argument(
        "--publish", action="store_true",
        help="Allow an explicit --season-year/--week run to POST predictions "
             "(default: explicit-week runs never post; live runs post normally).")
    run.add_argument(
        "--legacy-extraction", action="store_true",
        help="Use the original prototype SQL (live FranchiseSeasonMetric joins, "
             "leaky training) — parity comparison only.")
    run.add_argument(
        "--verbose", action="store_true", help="Debug-level logging.")

    backtest = subparsers.add_parser(
        "backtest",
        help="Predict a historical week as-of, grade against final scores. "
             "Never publishes.")
    backtest.add_argument(
        "--sport", type=normalize_sport, default="FootballNcaa",
        metavar="{FootballNcaa,FootballNfl}",
        help="Platform Sport enum name (case-insensitive).")
    backtest.add_argument("--season-year", type=int, required=True)
    backtest.add_argument("--week", type=int, required=True)
    backtest.add_argument(
        "--prior-season-tail", type=int, default=0, metavar="N",
        help="Same top-up semantics as run-week — and an experiment axis: "
             "grade a week with and without it and compare.")
    backtest.add_argument(
        "--dump-intermediate", action="store_true",
        help="Write the prediction artifacts to ./data alongside the grade.")
    backtest.add_argument(
        "--verbose", action="store_true", help="Debug-level logging.")

    args = parser.parse_args(argv)

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
    )

    if args.command == "backtest":
        try:
            result = run_backtest(
                sport=args.sport,
                season_year=args.season_year,
                week=args.week,
                prior_tail=args.prior_season_tail,
                dump_intermediate=args.dump_intermediate,
            )
            print()
            print(result.format())
            return 0
        except Exception as ex:  # noqa: BLE001 — CLI boundary
            logging.getLogger("metricbot").exception("Backtest failed: %s", ex)
            return 1

    if args.command == "run-week":
        try:
            return run_week(
                sport=args.sport,
                dry_run=args.dry_run,
                dump_intermediate=args.dump_intermediate,
                season_year=args.season_year,
                week=args.week,
                prior_tail=args.prior_season_tail,
                publish=args.publish,
                legacy_extraction=args.legacy_extraction,
            )
        except Exception as ex:  # noqa: BLE001 — CLI boundary: fail loudly, exit nonzero
            # .exception() so the traceback lands too: this is the only
            # diagnostic artifact for a scheduled weekly run.
            logging.getLogger("metricbot").exception("Pipeline failed: %s", ex)
            return 1

    return 2


if __name__ == "__main__":
    sys.exit(main())
