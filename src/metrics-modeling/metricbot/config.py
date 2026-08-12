"""Configuration for the MetricBot pipeline.

Resolution order per setting:
1. Environment variable (METRICBOT_*)
2. Optional key=value file `_metricbot.env` beside the existing
   `_common-variables.ps1` (same secrets directory the PowerShell flow
   already uses, but a plain env-file format Python can read — no
   PowerShell parsing)

Nothing here is ever committed: the repo is public and these are prod
credentials. In Phase B (K3s CronJob) the same variables arrive as
Kubernetes secrets.
"""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

# Sport values are the PLATFORM'S Sport enum names (SportsData.Core.
# Common.Sport) — deliberately NOT a second vocabulary. Databases are
# per-sport (the per-sport DB split is load-bearing platform-wide);
# football models cover both leagues with the same feature set.
#
# fbs_scope (v1.1 design): deetsMeter covers NCAAFB games with at least
# one FBS participant; NFL covers EVERY game. The flag drives both the
# slate filter (SQL) and the residual-model training subset (python).
SPORT_DATABASES = {
    "FootballNcaa": {"database": "sdProducer.FootballNcaa", "fbs_scope": True},
    "FootballNfl": {"database": "sdProducer.FootballNfl", "fbs_scope": False},
}

SUPPORTED_SPORTS = tuple(SPORT_DATABASES)


def normalize_sport(value: str) -> str:
    """Resolve a sport argument to its canonical Sport-enum name.

    Case-insensitive purely for CLI ergonomics — `--sport footballnfl`
    works — but there is only ONE vocabulary, not an alias set.
    """
    if value is None:
        raise SystemExit("A sport is required: " + ", ".join(SUPPORTED_SPORTS))
    match = next((s for s in SUPPORTED_SPORTS if s.lower() == value.strip().lower()), None)
    if match is None:
        raise SystemExit(
            f"Unknown sport '{value}'. MetricBot has football models only: "
            + ", ".join(SUPPORTED_SPORTS))
    return match

# The synthetic MetricBot user the API attributes predictions to
# (IsSynthetic = true; see design doc, Decision 6).
DEFAULT_METRICBOT_USER_ID = "b210d677-19c3-4f26-ac4b-b2cc7ad58c44"


def _load_env_file() -> dict[str, str]:
    secrets_path = os.environ.get("SPORTDEETS_SECRETS_PATH")
    if not secrets_path:
        return {}
    # SPORTDEETS_SECRETS_PATH historically points at the PowerShell
    # variables FILE (_common-variables.ps1); _metricbot.env lives beside
    # it (leading underscore = the secrets-file naming convention).
    # Accept either the file or its directory.
    base = Path(secrets_path)
    secrets_dir = base.parent if base.suffix else base
    env_path = secrets_dir / "_metricbot.env"
    if not env_path.is_file():
        return {}
    values: dict[str, str] = {}
    for line in env_path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        value = value.strip()
        # Tolerate optional surrounding quotes (standard .env convention).
        if len(value) >= 2 and value[0] == value[-1] and value[0] in ("'", '"'):
            value = value[1:-1]
        values[key.strip()] = value
    return values


@dataclass(frozen=True)
class Config:
    pg_host: str
    pg_user: str
    pg_password: str
    pg_database: str
    pg_port: str
    api_base_url: str
    admin_token: str
    metricbot_user_id: str
    # v1.1: True = slate restricted to FBS-participant games and the
    # residual model trains on the FBS∩priced subset (NCAAFB); False =
    # every game (NFL). Defaulted so test fixtures stay terse.
    fbs_scope: bool = True

    @staticmethod
    def load(sport: str) -> "Config":
        sport = normalize_sport(sport)

        file_values = _load_env_file()

        def get(name: str, default: str | None = None) -> str:
            value = os.environ.get(name) or file_values.get(name) or default
            if value is None:
                raise SystemExit(
                    f"Missing required setting {name}. Set it as an environment "
                    f"variable or in _metricbot.env beside your secrets file")
            return value

        return Config(
            pg_host=get("METRICBOT_PG_HOST"),
            pg_user=get("METRICBOT_PG_USER"),
            pg_password=get("METRICBOT_PG_PASSWORD"),
            pg_database=SPORT_DATABASES[sport]["database"],
            fbs_scope=SPORT_DATABASES[sport]["fbs_scope"],
            pg_port=get("METRICBOT_PG_PORT", "5432"),
            api_base_url=get("METRICBOT_API_BASE_URL").rstrip("/"),
            admin_token=get("METRICBOT_ADMIN_TOKEN"),
            metricbot_user_id=get("METRICBOT_USER_ID", DEFAULT_METRICBOT_USER_ID),
        )
