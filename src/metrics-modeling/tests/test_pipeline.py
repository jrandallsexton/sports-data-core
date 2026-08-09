"""Offline tests — no database, no network.

Covers the pieces that can drift silently: the model math's contracts,
the DTO shape the API ingestion endpoint expects, and config parsing.
Extraction is deliberately untested here (it shells to psql against a
real database; that's integration territory).
"""

from __future__ import annotations

import numpy as np
import pandas as pd
import pytest

from metricbot import MODEL_VERSION
from metricbot.config import Config, normalize_sport
from metricbot.dtos import PICKTYPE_ATS, PICKTYPE_STRAIGHT_UP, build_prediction_dtos
from metricbot.model import FEATURE_COLS, ModelError, predict_ats, predict_straight_up


def _frame(rows: int, completed: bool, seed: int = 42) -> pd.DataFrame:
    rng = np.random.default_rng(seed)
    data = {col: rng.normal(size=rows) for col in FEATURE_COLS}
    data["ContestId"] = [f"contest-{i}" for i in range(rows)]
    data["HomeFranchiseSeasonId"] = [f"home-{i}" for i in range(rows)]
    data["AwayFranchiseSeasonId"] = [f"away-{i}" for i in range(rows)]
    data["Spread"] = rng.normal(scale=7, size=rows).round(1)

    if completed:
        home = rng.integers(0, 50, rows)
        away = rng.integers(0, 50, rows)
        data["HomeScore"] = home
        data["AwayScore"] = away
        data["Winner"] = ["HOME" if h >= a else "AWAY" for h, a in zip(home, away)]
    else:
        data["HomeScore"] = [None] * rows
        data["AwayScore"] = [None] * rows
        data["Winner"] = [None] * rows

    return pd.DataFrame(data)


def test_straight_up_trains_only_on_decided_games():
    training = _frame(200, completed=True)
    # Slate rows carry no Winner and must never enter training.
    training = pd.concat([training, _frame(5, completed=False)], ignore_index=True)

    result = predict_straight_up(training, _frame(5, completed=False))

    assert result.training_rows == 200
    assert result.residual_std > 0
    assert result.mae > 0
    assert len(result.predictions) == 5


def test_win_probabilities_are_clipped_and_directional():
    su = predict_straight_up(_frame(200, completed=True), _frame(20, completed=False))
    probs = su.predictions["WinProbability"]

    assert probs.between(0.01, 0.99).all()
    # P(home wins) must move with the predicted margin.
    ordered = su.predictions.sort_values("PredictedMargin")
    assert ordered["WinProbability"].is_monotonic_increasing


def test_ats_probabilities_complement_and_respect_spread():
    su = predict_straight_up(_frame(200, completed=True), _frame(20, completed=False))
    predictions = predict_ats(su)

    total = predictions["HomeCoverProbability"] + predictions["AwayCoverProbability"]
    # Complementary up to the 0.01/0.99 clipping.
    assert ((total - 1.0).abs() < 0.02).all()
    assert predictions["AtsPredictedLabel"].isin([0, 1]).all()


def test_dtos_match_the_ingestion_contract():
    su = predict_straight_up(_frame(200, completed=True), _frame(7, completed=False))
    dtos = build_prediction_dtos(predict_ats(su))

    assert len(dtos) == 14  # SU + ATS per contest
    assert {d["PredictionType"] for d in dtos} == {PICKTYPE_STRAIGHT_UP, PICKTYPE_ATS}

    for dto in dtos:
        # Exactly the keys ContestPredictionDto binds.
        assert set(dto) == {
            "ContestId", "WinnerFranchiseSeasonId", "WinProbability",
            "PredictionType", "ModelVersion",
        }
        assert dto["ModelVersion"] == MODEL_VERSION
        assert 0.01 <= dto["WinProbability"] <= 0.99
        # Both prediction types are expressed home-relative for the UI.
        assert dto["WinnerFranchiseSeasonId"].startswith("home-")


def test_config_rejects_unknown_sport():
    with pytest.raises(SystemExit):
        Config.load("cricket")


def test_sport_vocabulary_matches_the_platform_enum():
    # One vocabulary end to end: MetricBot speaks SportsData.Core.Common
    # .Sport names, not an invented short form.
    assert normalize_sport("FootballNcaa") == "FootballNcaa"
    assert normalize_sport("FootballNfl") == "FootballNfl"
    # Case-insensitive for CLI ergonomics only.
    assert normalize_sport("footballnfl") == "FootballNfl"
    # The old invented short forms are gone, not aliased.
    with pytest.raises(SystemExit):
        normalize_sport("ncaaf")
    with pytest.raises(SystemExit):
        normalize_sport("nfl")


def test_env_file_parsing_tolerates_quotes_and_comments(tmp_path, monkeypatch):
    secrets = tmp_path / "_metricbot.env"
    secrets.write_text(
        "# comment line\n"
        'METRICBOT_PG_HOST="db.example.com"\n'
        "METRICBOT_PG_USER=bob\n"
        "METRICBOT_PG_PASSWORD='p@ss=word'\n"
        "\n",
        encoding="utf-8",
    )
    # SPORTDEETS_SECRETS_PATH may point at the PowerShell secrets FILE,
    # not its directory — the loader handles both.
    monkeypatch.setenv("SPORTDEETS_SECRETS_PATH", str(tmp_path / "_common-variables.ps1"))

    from metricbot import config as config_module

    values = config_module._load_env_file()
    assert values["METRICBOT_PG_HOST"] == "db.example.com"
    assert values["METRICBOT_PG_USER"] == "bob"
    assert values["METRICBOT_PG_PASSWORD"] == "p@ss=word"


def test_empty_training_window_is_rejected():
    # Early-season without a prior-season tail: no decided games at all.
    empty = _frame(0, completed=True)
    with pytest.raises(ModelError, match="nothing to train on"):
        predict_straight_up(empty, _frame(3, completed=False))


def test_underdetermined_training_window_is_rejected():
    # Fewer decided games than features would interpolate exactly,
    # collapsing residual_std to 0 and making probabilities meaningless.
    too_few = _frame(len(FEATURE_COLS) - 1, completed=True)
    with pytest.raises(ModelError, match="underdetermined"):
        predict_straight_up(too_few, _frame(3, completed=False))
