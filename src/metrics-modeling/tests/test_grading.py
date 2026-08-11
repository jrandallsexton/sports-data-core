"""Offline tests for the grading rules — every edge the harness spec
calls out: pushes, ties, missing spreads, missing scores, baselines."""

from __future__ import annotations

import subprocess
from types import SimpleNamespace

import pandas as pd
import pytest

from metricbot import extract
from metricbot.config import Config
from metricbot.grading import ATS_BREAK_EVEN, format_report, grade_week


def _prediction(contest_id, p_home, p_cover, margin, spread):
    return {
        "ContestId": contest_id,
        "PredictedMargin": margin,
        "WinProbability": p_home,
        "HomeCoverProbability": p_cover,
        "Spread": spread,
        # The as-of slate carries literal NULL score columns.
        "HomeScore": None,
        "AwayScore": None,
    }


def _score(contest_id, home, away):
    return {"ContestId": contest_id, "HomeScore": home, "AwayScore": away}


def test_su_and_ats_grading_with_baselines():
    predictions = pd.DataFrame([
        # Home favored (-7), model picks home, home wins by 10: SU hit, ATS hit.
        _prediction("g1", 0.75, 0.60, 8.0, -7.0),
        # Away favored (+3), model picks home, away wins by 7: SU miss;
        # ATS: model picked away cover (0.30 < 0.5), away covered: hit.
        _prediction("g2", 0.60, 0.30, 2.0, 3.0),
        # Home favored (-3), model picks away, home wins by 1: SU miss;
        # ATS: model picked away cover, cover margin 1-3 < 0, away covered: hit.
        _prediction("g3", 0.40, 0.20, -2.0, -3.0),
    ])
    scores = pd.DataFrame([
        _score("g1", 31, 21),
        _score("g2", 14, 21),
        _score("g3", 22, 21),
    ])

    report = grade_week(predictions, scores)

    assert report.graded == 3 and report.ungradeable == 0
    assert report.su["decided"] == 3
    assert report.su["correct"] == 1
    assert report.su["accuracy"] == round(1 / 3, 4)
    # Home won g1 and g3: always-home baseline 2/3.
    assert report.su["baseline_always_home"] == round(2 / 3, 4)
    # Favorite: g1 home fav won (hit), g2 away fav won (hit), g3 home fav won (hit).
    assert report.su["baseline_favorite"]["accuracy"] == 1.0
    # Same-games model accuracy: all three games have spreads, so it
    # equals overall accuracy here (1/3); no spreadless games.
    assert report.su["baseline_favorite"]["model_accuracy_same_games"] == round(1 / 3, 4)
    assert report.su["spreadless"] == {"games": 0, "model_accuracy": None}
    assert report.ats["decided"] == 3
    assert report.ats["correct"] == 3
    assert report.ats["break_even_reference"] == ATS_BREAK_EVEN


def test_pushes_are_excluded_and_counted():
    predictions = pd.DataFrame([
        # Home -7, wins by exactly 7: PUSH.
        _prediction("g1", 0.70, 0.55, 7.0, -7.0),
        _prediction("g2", 0.70, 0.55, 7.0, -7.0),
    ])
    scores = pd.DataFrame([
        _score("g1", 28, 21),   # margin 7, cover margin 0 -> push
        _score("g2", 28, 14),   # margin 14 -> home covers
    ])

    report = grade_week(predictions, scores)

    assert report.ats["pushes_excluded"] == 1
    assert report.ats["decided"] == 1
    assert report.ats["correct"] == 1
    # The push still grades SU normally.
    assert report.su["decided"] == 2


def test_ties_missing_spreads_and_unplayed_games():
    predictions = pd.DataFrame([
        _prediction("tie", 0.55, 0.55, 1.0, -1.0),
        _prediction("nospread", 0.80, 0.80, 14.0, None),
        _prediction("unplayed", 0.60, 0.60, 3.0, -3.0),
    ])
    scores = pd.DataFrame([
        _score("tie", 20, 20),
        _score("nospread", 35, 10),
        # "unplayed" absent: no final score.
    ])

    report = grade_week(predictions, scores)

    assert report.ungradeable == 1
    assert report.su["ties_excluded"] == 1
    assert report.su["decided"] == 1          # only nospread decided
    assert report.ats["no_spread_ungraded"] == 1
    assert report.ats["decided"] == 1          # only the tie game had a spread
    # Tie with spread -1: cover margin 0 + -1 = -1, away covered; model
    # picked home cover (0.55): ATS miss.
    assert report.ats["correct"] == 0


def test_margin_model_vs_market():
    predictions = pd.DataFrame([
        # Model says home by 10, market says home by 7 (spread -7), actual 14.
        _prediction("g1", 0.75, 0.60, 10.0, -7.0),
    ])
    scores = pd.DataFrame([_score("g1", 28, 14)])

    report = grade_week(predictions, scores)

    assert report.margin["model_mae"] == 4.0    # |10 - 14|
    assert report.margin["market_mae"] == 7.0   # |-(-7) - 14|
    assert report.margin["market_games"] == 1


def test_calibration_buckets_predicted_vs_actual():
    rows = (
        [_prediction(f"hi{i}", 0.85, 0.5, 10.0, -7.0) for i in range(4)]
        + [_prediction(f"lo{i}", 0.35, 0.5, -3.0, 3.0) for i in range(2)]
    )
    predictions = pd.DataFrame(rows)
    scores = pd.DataFrame(
        # 3 of 4 high-confidence homes win; both low-confidence homes lose.
        [_score(f"hi{i}", 30, 20) for i in range(3)] + [_score("hi3", 17, 20)]
        + [_score(f"lo{i}", 10, 24) for i in range(2)]
    )

    report = grade_week(predictions, scores)

    by_bucket = {b["bucket"]: b for b in report.calibration}
    assert by_bucket["0.8-0.9"]["games"] == 4
    assert by_bucket["0.8-0.9"]["actual"] == 0.75
    assert by_bucket["0.3-0.4"]["games"] == 2
    assert by_bucket["0.3-0.4"]["actual"] == 0.0


def test_format_report_renders_every_section():
    predictions = pd.DataFrame([_prediction("g1", 0.75, 0.60, 8.0, -7.0)])
    scores = pd.DataFrame([_score("g1", 31, 21)])

    text = format_report(grade_week(predictions, scores), "BACKTEST test")

    assert "STRAIGHT UP" in text
    assert "ATS" in text
    assert "MARGIN" in text
    assert "CALIBRATION" in text
    assert "break-even 52.4%" in text


def test_all_ties_reports_no_data_instead_of_nan():
    predictions = pd.DataFrame([
        _prediction("t1", 0.60, 0.55, 2.0, -1.0),
        _prediction("t2", 0.45, 0.40, -1.0, 2.0),
    ])
    scores = pd.DataFrame([_score("t1", 17, 17), _score("t2", 20, 20)])

    report = grade_week(predictions, scores)

    assert report.su["decided"] == 0
    assert report.su["ties_excluded"] == 2
    assert report.su["accuracy"] is None
    assert report.su["brier"] is None
    assert report.su["baseline_always_home"] is None

    text = format_report(report, "BACKTEST all-ties")
    assert "nan" not in text.lower()
    assert "no decided games" in text


def test_zero_completed_games_is_all_ungradeable():
    # Backtesting a week nothing has finished in: the scores extraction
    # legitimately returns an empty frame (allow_empty), and the report
    # says so instead of erroring.
    predictions = pd.DataFrame([
        _prediction("g1", 0.70, 0.55, 7.0, -7.0),
        _prediction("g2", 0.60, 0.45, 3.0, -3.0),
    ])
    scores = pd.DataFrame(columns=["ContestId", "HomeScore", "AwayScore"])

    report = grade_week(predictions, scores)

    assert report.week_contests == 2
    assert report.graded == 0
    assert report.ungradeable == 2
    assert report.su == {} and report.ats == {} and report.margin == {}

    text = format_report(report, "BACKTEST empty")
    assert "0 graded" in text


def _fake_config() -> Config:
    return Config(
        pg_host="db.example.invalid", pg_user="u", pg_password="p",
        pg_database="d", pg_port="5432",
        api_base_url="http://api.example.invalid",
        admin_token="t", metricbot_user_id="00000000-0000-0000-0000-000000000000")


def test_extract_final_scores_allows_header_only_result(monkeypatch):
    # psql --csv emits the header row even for zero-row results; a week
    # nothing has finished in must come back as an empty frame, not an
    # ExtractionError.
    def fake_run(*_args: object, **_kwargs: object) -> SimpleNamespace:
        return SimpleNamespace(
            returncode=0, stdout="ContestId,HomeScore,AwayScore\n", stderr="")

    monkeypatch.setattr(subprocess, "run", fake_run)

    frame = extract.extract_final_scores(_fake_config(), 2026, 1)

    assert frame.empty
    assert list(frame.columns) == ["ContestId", "HomeScore", "AwayScore"]


def test_extract_training_still_rejects_empty_results(monkeypatch):
    # The default (allow_empty=False) behavior is unchanged: an empty
    # training extraction is an error, not a silent no-op.
    def fake_run(*_args: object, **_kwargs: object) -> SimpleNamespace:
        return SimpleNamespace(returncode=0, stdout="A,B\n", stderr="")

    monkeypatch.setattr(subprocess, "run", fake_run)

    with pytest.raises(extract.ExtractionError, match="zero rows"):
        extract.extract_training(_fake_config())


def test_spread_restricted_su_separates_model_from_easy_spreadless_games():
    predictions = pd.DataFrame([
        # Two spread games: model splits them (one hit, one miss).
        _prediction("s1", 0.70, 0.55, 7.0, -7.0),    # home wins: hit
        _prediction("s2", 0.70, 0.55, 7.0, -7.0),    # away wins: miss
        # Two spreadless mismatches: model nails both.
        _prediction("e1", 0.95, 0.5, 30.0, None),    # home blowout: hit
        _prediction("e2", 0.05, 0.5, -30.0, None),   # away blowout: hit
    ])
    scores = pd.DataFrame([
        _score("s1", 28, 14),
        _score("s2", 14, 28),
        _score("e1", 55, 3),
        _score("e2", 0, 45),
    ])

    report = grade_week(predictions, scores)

    # Overall 3/4 is flattered by the easy games; the same-games number
    # tells the honest story: 1/2 on games the market actually priced.
    assert report.su["accuracy"] == 0.75
    fav = report.su["baseline_favorite"]
    assert fav["games_with_spread"] == 2
    assert fav["model_accuracy_same_games"] == 0.5
    assert report.su["spreadless"] == {"games": 2, "model_accuracy": 1.0}

    text = format_report(report, "BACKTEST split")
    assert "model 50.0% vs favorite" in text
    assert "spreadless games: model 100.0%" in text
