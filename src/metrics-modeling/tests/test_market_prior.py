"""v1.1 market-prior tests — the residual model, the is_priced
predicate, the fallback threshold, and the FBS training carve."""

from __future__ import annotations

import numpy as np
import pandas as pd

from metricbot import MODEL_VERSION
from metricbot.dtos import PICKTYPE_ATS, PICKTYPE_STRAIGHT_UP, build_prediction_dtos
from metricbot.model import (FEATURE_COLS, MIN_RESIDUAL_ROWS, is_priced,
                             predict_market_prior)


def _frame(rows: int, completed: bool, seed: int = 42,
           spread: bool = True, fbs: bool = True) -> pd.DataFrame:
    rng = np.random.default_rng(seed)
    data = {col: rng.normal(size=rows) for col in FEATURE_COLS}
    data["ContestId"] = [f"c{seed}-{i}" for i in range(rows)]
    data["HomeFranchiseSeasonId"] = [f"h{i}" for i in range(rows)]
    data["AwayFranchiseSeasonId"] = [f"a{i}" for i in range(rows)]
    data["Spread"] = (rng.normal(scale=7, size=rows).round(1) if spread
                      else np.full(rows, np.nan))
    data["FbsParticipant"] = ["t" if fbs else "f"] * rows

    if completed:
        home = rng.integers(0, 50, rows)
        away = rng.integers(0, 50, rows)
        data["HomeScore"] = home.astype(float)
        data["AwayScore"] = away.astype(float)
        data["Winner"] = ["HOME" if h >= a else "AWAY" for h, a in zip(home, away)]
    else:
        data["HomeScore"] = np.full(rows, np.nan)
        data["AwayScore"] = np.full(rows, np.nan)
        data["Winner"] = [None] * rows
    return pd.DataFrame(data)


def _training(priced_rows: int = 400, unpriced_rows: int = 200) -> pd.DataFrame:
    return pd.concat([
        _frame(priced_rows, completed=True, seed=1, spread=True),
        _frame(unpriced_rows, completed=True, seed=2, spread=False),
    ], ignore_index=True)


def test_is_priced_treats_pickem_as_priced():
    frame = pd.DataFrame({"Spread": [None, 0.0, -7.0]})
    assert list(is_priced(frame)) == [False, True, True]


def test_priced_rows_use_market_prior_and_unpriced_fall_back():
    slate = pd.concat([
        _frame(6, completed=False, seed=3, spread=True),
        _frame(4, completed=False, seed=4, spread=False),
    ], ignore_index=True)

    result = predict_market_prior(_training(), slate, fbs_scope=False)
    p = result.predictions

    assert result.residual_rows == 400
    assert (p.loc[is_priced(p), "ModelPath"] == "residual").all()
    assert (p.loc[~is_priced(p), "ModelPath"] == "fallback").all()
    # Market prior: predicted margin equals -Spread + correction, so
    # margin + Spread (the implied edge) must be identical to the
    # correction — verified indirectly: cover prob > 0.5 iff implied
    # edge > 0.
    priced = p[is_priced(p)]
    edge = priced["PredictedMargin"] + priced["Spread"]
    assert ((priced["HomeCoverProbability"] > 0.5) == (edge > 0)).all()


def test_uninformative_correction_hugs_the_market_with_low_confidence():
    # Train on data where the market is right ON AVERAGE (residual is
    # pure noise, uncorrelated with the features): the correction has
    # nothing to learn, so predictions must hug the prior and ATS
    # confidence must collapse toward 50% — the design's honest-output
    # property. (An EXACT-zero-residual fixture degenerates: float
    # non-associativity leaves ~1e-15 residuals and the probability
    # math runs on noise-scale std.)
    # Corpus sized to production's features-to-rows ratio (~3.5k rows /
    # 64 features): at 300 rows, correction overfit alone produced
    # cover probabilities up to 0.92 from pure noise — worth knowing,
    # and exactly why MIN_RESIDUAL_ROWS exists — but that is not the
    # property under test here.
    rng = np.random.default_rng(99)
    training = _frame(2000, completed=True, seed=5, spread=True)
    noise = rng.normal(scale=3.0, size=len(training))
    training["HomeScore"] = 30.0
    training["AwayScore"] = 30.0 + training["Spread"] + noise
    margin = training["HomeScore"] - training["AwayScore"]
    training["Winner"] = ["HOME" if m > 0 else "AWAY" for m in margin]

    slate = _frame(8, completed=False, seed=6, spread=True)
    result = predict_market_prior(training, slate, fbs_scope=False)
    priced = result.predictions

    # Margin stays near the market's number (correction fit on noise
    # contributes only overfit-scale wiggle)...
    assert np.allclose(priced["PredictedMargin"], -priced["Spread"], atol=2.5)
    # ...and no confident ATS opinions emerge from nothing.
    assert (priced["HomeCoverProbability"].between(0.25, 0.75)).all()


def test_thin_residual_corpus_falls_back_entirely():
    thin = pd.concat([
        _frame(MIN_RESIDUAL_ROWS - 10, completed=True, seed=7, spread=True),
        _frame(400, completed=True, seed=8, spread=False),
    ], ignore_index=True)
    slate = _frame(5, completed=False, seed=9, spread=True)

    result = predict_market_prior(thin, slate, fbs_scope=False)

    assert result.residual_rows == 0
    assert result.residual_model_std is None
    assert (result.predictions["ModelPath"] == "fallback").all()


def test_fbs_scope_carves_the_residual_corpus():
    training = pd.concat([
        _frame(400, completed=True, seed=10, spread=True, fbs=True),
        _frame(300, completed=True, seed=11, spread=True, fbs=False),
    ], ignore_index=True)
    slate = _frame(5, completed=False, seed=12, spread=True)

    scoped = predict_market_prior(training, slate, fbs_scope=True)
    unscoped = predict_market_prior(training, slate, fbs_scope=False)

    assert scoped.residual_rows == 400      # FBS∩priced only
    assert unscoped.residual_rows == 700    # NFL: every priced game


def test_dtos_emit_ats_only_for_priced_contests():
    slate = pd.concat([
        _frame(3, completed=False, seed=13, spread=True),
        _frame(2, completed=False, seed=14, spread=False),
    ], ignore_index=True)
    result = predict_market_prior(_training(), slate, fbs_scope=False)

    dtos = build_prediction_dtos(result.predictions)

    su = [d for d in dtos if d["PredictionType"] == PICKTYPE_STRAIGHT_UP]
    ats = [d for d in dtos if d["PredictionType"] == PICKTYPE_ATS]
    assert len(su) == 5                      # every contest
    assert len(ats) == 3                     # priced contests only
    assert all(d["ModelVersion"] == MODEL_VERSION for d in dtos)
    # The NaN defect is dead: every probability is a real number.
    assert all(0.01 <= d["WinProbability"] <= 0.99 for d in dtos)


def test_probability_scale_is_out_of_sample_not_fit_std():
    # With more features than signal, in-sample fit std shrinks below the
    # honest error; the deployed scale must come from out-of-fold
    # residuals. Train on pure noise and verify the used std is close to
    # the TRUE noise scale (3.0) rather than the optimistic fit std.
    rng = np.random.default_rng(7)
    training = _frame(2000, completed=True, seed=8, spread=True)
    noise = rng.normal(scale=3.0, size=len(training))
    training["HomeScore"] = 30.0
    training["AwayScore"] = 30.0 + training["Spread"] + noise
    margin = training["HomeScore"] - training["AwayScore"]
    training["Winner"] = ["HOME" if m > 0 else "AWAY" for m in margin]

    result = predict_market_prior(
        training, _frame(5, completed=False, seed=9, spread=True), fbs_scope=False)

    # The in-sample fit std — what v1.1.0 wrongly used — computed the
    # same way the model would: full-corpus fit, residuals against it.
    from sklearn.linear_model import LinearRegression
    from metricbot.model import is_priced
    corpus = training[training["Winner"].isin(["HOME", "AWAY"])]
    corpus = corpus[is_priced(corpus)]
    x = corpus[FEATURE_COLS].fillna(0)
    y = corpus["HomeScore"] - corpus["AwayScore"] + corpus["Spread"]
    fit = LinearRegression().fit(x, y)
    fit_std = float(np.std(y - fit.predict(x)))

    # The deployed scale must exceed the optimistic in-sample number and
    # land at (or slightly above) the TRUE noise scale of 3.0.
    assert result.residual_model_std is not None
    assert result.residual_model_std > fit_std
    assert 2.9 <= result.residual_model_std <= 3.6
