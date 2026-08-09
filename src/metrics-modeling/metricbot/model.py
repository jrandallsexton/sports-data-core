"""SU + ATS math, ported VERBATIM from the prototype scripts.

Sources of truth: predict_straightup.py, predict_ats.py. Any intentional
change to this math must bump MODEL_VERSION in __init__.py. The
prototype's combine step (concat train + current week, then filter to
completed games) is preserved semantically: filtering on
Winner in {HOME, AWAY} excludes the score-less current-week rows, so
training input is identical either way.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import pandas as pd
from scipy.stats import norm
from sklearn.linear_model import LinearRegression

from . import MODEL_VERSION

# The 64 features from predict_straightup.py — order preserved.
FEATURE_COLS = [
    'HomeYpp', 'HomeSuccessRate', 'HomeExplosiveRate', 'HomePointsPerDrive',
    'HomeThirdFourthRate', 'HomeRzTdRate', 'HomeRzScoreRate', 'HomeTimePossRatio',
    'HomeOppYpp', 'HomeOppSuccessRate', 'HomeOppExplosiveRate', 'HomeOppPointsPerDrive',
    'HomeOppThirdFourthRate', 'HomeOppRzTdRate', 'HomeOppScoreTdRate',
    'HomeNetPunt', 'HomeFgPctShrunk', 'HomeFieldPosDiff', 'HomeTurnoverMarginPerDrive',
    'HomePenaltyYardsPerPlay',
    'HomePtsScoredAvg', 'HomePtsScoredMin', 'HomePtsScoredMax',
    'HomePtsAllowedAvg', 'HomePtsAllowedMin', 'HomePtsAllowedMax',
    'HomeMarginWinAvg', 'HomeMarginWinMin', 'HomeMarginWinMax',
    'HomeMarginLossAvg', 'HomeMarginLossMin', 'HomeMarginLossMax',

    'AwayYpp', 'AwaySuccessRate', 'AwayExplosiveRate', 'AwayPointsPerDrive',
    'AwayThirdFourthRate', 'AwayRzTdRate', 'AwayRzScoreRate', 'AwayTimePossRatio',
    'AwayOppYpp', 'AwayOppSuccessRate', 'AwayOppExplosiveRate', 'AwayOppPointsPerDrive',
    'AwayOppThirdFourthRate', 'AwayOppRzTdRate', 'AwayOppScoreTdRate',
    'AwayNetPunt', 'AwayFgPctShrunk', 'AwayFieldPosDiff', 'AwayTurnoverMarginPerDrive',
    'AwayPenaltyYardsPerPlay',
    'AwayPtsScoredAvg', 'AwayPtsScoredMin', 'AwayPtsScoredMax',
    'AwayPtsAllowedAvg', 'AwayPtsAllowedMin', 'AwayPtsAllowedMax',
    'AwayMarginWinAvg', 'AwayMarginWinMin', 'AwayMarginWinMax',
    'AwayMarginLossAvg', 'AwayMarginLossMin', 'AwayMarginLossMax'
]


class ModelError(RuntimeError):
    """Training data cannot support a usable model."""


@dataclass
class SuResult:
    predictions: pd.DataFrame  # ContestId, Home/AwayFranchiseSeasonId, PredictedMargin, WinProbability, ...
    mae: float
    residual_std: float
    training_rows: int


def predict_straight_up(training: pd.DataFrame, current_week: pd.DataFrame) -> SuResult:
    """LinearRegression on point margin (home − away); win probability is
    the normal-tail P(margin > 0) using the residual std."""
    train = training[training["Winner"].isin(["HOME", "AWAY"])].copy()
    train["Margin"] = train["HomeScore"] - train["AwayScore"]

    # Degenerate inputs would produce confident-looking nonsense: an
    # underdetermined fit (more features than games) interpolates its
    # training data exactly, driving residual_std to 0 — and a 0 scale is
    # not a valid normal distribution, so every probability would come
    # back as 0 or 1 and clipping would merely disguise it.
    if len(train) == 0:
        raise ModelError(
            "No completed games with a decided winner in the training window — "
            "nothing to train on. For early-season runs, use --prior-season-tail.")
    if len(train) <= len(FEATURE_COLS):
        raise ModelError(
            f"Training window has {len(train)} decided games for {len(FEATURE_COLS)} "
            f"features — underdetermined. Widen the window or use --prior-season-tail.")

    x_train = train[FEATURE_COLS].fillna(0)
    y_train = train["Margin"]

    model = LinearRegression()
    model.fit(x_train, y_train)

    residuals = y_train - model.predict(x_train)
    residual_std = float(np.std(residuals))
    mae = float(np.mean(np.abs(residuals)))

    if not np.isfinite(residual_std) or residual_std <= 0:
        raise ModelError(
            f"Residual standard deviation is {residual_std} — the fit is degenerate, "
            f"so win probabilities would be meaningless.")

    predictions = current_week.copy()
    x_predict = predictions[FEATURE_COLS].fillna(0)
    predictions["PredictedMargin"] = model.predict(x_predict)
    predictions["WinProbability"] = norm.sf(
        0, loc=predictions["PredictedMargin"], scale=residual_std)
    predictions["WinProbability"] = predictions["WinProbability"].clip(0.01, 0.99)
    predictions["PredictedLabel"] = (predictions["WinProbability"] > 0.5).astype(int)
    predictions["ModelVersion"] = MODEL_VERSION
    predictions["ResidualStd"] = residual_std

    return SuResult(
        predictions=predictions,
        mae=mae,
        residual_std=residual_std,
        training_rows=len(train),
    )


def predict_ats(su: SuResult) -> pd.DataFrame:
    """P(home covers) = P(margin + spread > 0) under the same residual
    distribution. Spread is home-relative (negative = home favored)."""
    predictions = su.predictions.copy()

    margin_vs_spread = predictions["PredictedMargin"] + predictions["Spread"]
    predictions["HomeCoverProbability"] = norm.sf(
        0, loc=margin_vs_spread, scale=su.residual_std).clip(0.01, 0.99)
    predictions["AwayCoverProbability"] = norm.sf(
        0, loc=-margin_vs_spread, scale=su.residual_std).clip(0.01, 0.99)
    predictions["AtsPredictedLabel"] = (
        predictions["HomeCoverProbability"] > predictions["AwayCoverProbability"]
    ).astype(int)

    return predictions
