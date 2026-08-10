"""Data extraction via psql (the pipeline's only external tool).

Deliberately shells out to psql rather than adding a Postgres driver:
the operator box already has psql (the PowerShell flow required it), the
venv stays unchanged, and the Phase B container just installs
postgresql-client. Swap for psycopg later if a driver earns its way in.

The SQL files in ./sql are UNCHANGED from the prototype — they are the
spec. competition_metrics_current_week.sql self-detects the current week
via its own CTE, so no week parameter exists anywhere in extraction.
"""

from __future__ import annotations

import io
import os
import subprocess
from pathlib import Path

import pandas as pd

from .config import Config

SQL_DIR = Path(__file__).resolve().parent.parent / "sql"

# A blocked connection or runaway query must not pin a service worker
# forever. Full-corpus training extraction runs in seconds; 10 minutes is
# generous headroom before we call it stuck.
PSQL_TIMEOUT_SECONDS = 600

TRAINING_SQL = SQL_DIR / "competition_metrics_training.sql"
CURRENT_WEEK_SQL = SQL_DIR / "competition_metrics_current_week.sql"
ASOF_TRAINING_SQL = SQL_DIR / "competition_metrics_asof_training.sql"
ASOF_WEEK_SQL = SQL_DIR / "competition_metrics_asof_week.sql"
DETECT_WEEK_SQL = SQL_DIR / "detect_current_season_week.sql"
GRADING_SCORES_SQL = SQL_DIR / "grading_scores.sql"


class ExtractionError(RuntimeError):
    pass


def _run_psql(config: Config, sql_file: Path, variables: dict[str, int] | None = None) -> pd.DataFrame:
    if not sql_file.is_file():
        raise ExtractionError(f"SQL file not found: {sql_file}")

    env = os.environ.copy()
    env["PGPASSWORD"] = config.pg_password

    args = [
        "psql",
        "-h", config.pg_host,
        "-p", config.pg_port,
        "-U", config.pg_user,
        "-d", config.pg_database,
        "-f", str(sql_file),
        "--csv",
        "-v", "ON_ERROR_STOP=1",
    ]
    for name, value in (variables or {}).items():
        args += ["-v", f"{name}={int(value)}"]  # int() — nothing user-shaped reaches psql

    try:
        result = subprocess.run(
            args,
            env=env,
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=PSQL_TIMEOUT_SECONDS,
            check=False,
        )
    except subprocess.TimeoutExpired as ex:
        raise ExtractionError(
            f"psql exceeded {PSQL_TIMEOUT_SECONDS}s running {sql_file.name} — "
            f"database unreachable or query stuck.") from ex

    if result.returncode != 0:
        raise ExtractionError(
            f"psql failed for {sql_file.name} (exit {result.returncode}):\n{result.stderr.strip()}")

    frame = pd.read_csv(io.StringIO(result.stdout))
    if frame.empty:
        raise ExtractionError(
            f"{sql_file.name} returned zero rows — is the season week window open "
            f"and are metrics generated for {config.pg_database}?")
    return frame


def extract_training(config: Config) -> pd.DataFrame:
    """Completed games with scores/winner + both teams' season metrics."""
    return _run_psql(config, TRAINING_SQL)


def extract_current_week(config: Config) -> pd.DataFrame:
    """The current week's unplayed slate (self-detected by the SQL)."""
    return _run_psql(config, CURRENT_WEEK_SQL)


def extract_asof_training(config: Config, season_year: int, week: int) -> pd.DataFrame:
    """Option-B as-of training set: strictly before (season, week); the 12
    Pts/Margin columns are ENTERING-GAME windows, not live season rows."""
    return _run_psql(config, ASOF_TRAINING_SQL,
                     {"season_year": season_year, "week": week})


def extract_asof_week(config: Config, season_year: int, week: int, prior_tail: int) -> pd.DataFrame:
    """The (season, week) slate with features computed entering that week;
    prior_tail > 0 tops up thin early-week windows with the team's most
    recent prior-season (regular/post) games."""
    return _run_psql(config, ASOF_WEEK_SQL,
                     {"season_year": season_year, "week": week, "prior_tail": prior_tail})


def extract_final_scores(config: Config, season_year: int, week: int) -> pd.DataFrame:
    """Final scores for grading: the (season, week) slate's completed
    games — ContestId, HomeScore, AwayScore."""
    return _run_psql(config, GRADING_SCORES_SQL,
                     {"season_year": season_year, "week": week})


def detect_current_season_week(config: Config) -> tuple[int, int]:
    """Resolve NOW to (season_year, week) for live runs — the current open
    week window, or the next upcoming non-preseason week."""
    try:
        frame = _run_psql(config, DETECT_WEEK_SQL)
    except ExtractionError as ex:
        raise ExtractionError(
            "Could not resolve the current season week — no open or upcoming "
            "week window found. For off-season/system-testing runs pass "
            "--season-year and --week explicitly.") from ex
    return int(frame["SeasonYear"].iloc[0]), int(frame["WeekNumber"].iloc[0])
