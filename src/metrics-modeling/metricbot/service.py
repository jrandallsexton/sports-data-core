"""FastAPI wrapper — the same run_week() the CLI calls, over HTTP.

Deployment shape (Phase B, revised 2026-08-09): metricbot runs as an
INTERNAL service (no public ingress, same posture as Producer/Provider).
The API's Hangfire owns scheduling and manual triggering, so this service
stays a thin, stateless executor:

    POST /run-week   → run the pipeline, return DTOs + run metadata
    GET  /health     → liveness/readiness

Runs are seconds, so /run-week is synchronous and returns its results on
the wire — which also keeps experiment artifacts out of an ephemeral
container's filesystem.
"""

from __future__ import annotations

import logging

from typing import Literal

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from . import MODEL_VERSION
from .pipeline import RunResult, run_week

logger = logging.getLogger("metricbot.service")

app = FastAPI(
    title="MetricBot",
    version=MODEL_VERSION,
    description="deetsMeter prediction pipeline (internal service).",
)


class RunWeekRequest(BaseModel):
    # The platform's Sport enum names — one vocabulary end to end, so
    # the C# client sends Sport.ToString() with no translation layer.
    sport: Literal["FootballNcaa", "FootballNfl"] = "FootballNcaa"

    # Explicit (season, week) = experiment/backtest; omit both for a live
    # run that auto-resolves the current week.
    season_year: int | None = Field(default=None, ge=1990, le=2100)
    week: int | None = Field(default=None, ge=1, le=25)

    prior_season_tail: int = Field(default=0, ge=0, le=25)

    # Explicit-week runs never publish unless this is true; live runs
    # publish unless dry_run is true. Same guard as the CLI.
    publish: bool = False
    dry_run: bool = False

    # Return the prediction DTOs in the response body (experiments want
    # them; the weekly job doesn't need the payload back).
    include_dtos: bool = False


class RunWeekResponse(BaseModel):
    model_version: str
    sport: str
    # Always resolved: live runs detect it from the calendar, explicit
    # runs supply it.
    season_year: int
    week: int
    prior_season_tail: int
    training_rows: int
    contests: int
    mae: float
    residual_std: float
    published: bool
    elapsed_seconds: float
    dtos: list[dict] | None = None


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "healthy", "modelVersion": MODEL_VERSION}


@app.post("/run-week", response_model=RunWeekResponse)
def post_run_week(request: RunWeekRequest) -> RunWeekResponse:
    if (request.season_year is None) != (request.week is None):
        raise HTTPException(
            status_code=400,
            detail="season_year and week must be provided together (or both omitted for a live run).")

    try:
        result: RunResult = run_week(
            sport=request.sport,
            dry_run=request.dry_run,
            dump_intermediate=False,  # containers are ephemeral; results ride the response
            season_year=request.season_year,
            week=request.week,
            prior_tail=request.prior_season_tail,
            publish=request.publish,
            return_result=True,
        )
    except SystemExit as ex:  # config/validation failures raise SystemExit
        raise HTTPException(status_code=400, detail=str(ex)) from ex
    except Exception as ex:  # noqa: BLE001 — service boundary
        logger.exception("run-week failed")
        raise HTTPException(status_code=500, detail=f"{type(ex).__name__}: {ex}") from ex

    return RunWeekResponse(
        model_version=MODEL_VERSION,
        sport=request.sport,
        season_year=result.season_year,
        week=result.week,
        prior_season_tail=request.prior_season_tail,
        training_rows=result.training_rows,
        contests=result.contests,
        mae=result.mae,
        residual_std=result.residual_std,
        published=result.published,
        elapsed_seconds=result.elapsed_seconds,
        dtos=result.dtos if request.include_dtos else None,
    )
