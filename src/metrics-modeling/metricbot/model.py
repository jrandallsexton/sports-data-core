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


def is_priced(frame: pd.DataFrame) -> pd.Series:
    """The canonical pricing predicate (v1.1 design): a game is priced
    iff it has a spread — and a pick'em line (Spread == 0) IS priced;
    it is a real market opinion. One definition, used by the
    residual/fallback split, ATS DTO emission, and grading."""
    return frame["Spread"].notna()


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


# Fewer decided rows than this and the residual model is skipped in
# favor of the pure-stats fallback for the whole slate (e.g. early-2022
# backtests, before odds coverage begins).
MIN_RESIDUAL_ROWS = 3 * len(FEATURE_COLS)


@dataclass
class V11Result:
    """v1.1 (market-prior) predictions plus fit diagnostics."""
    predictions: pd.DataFrame
    training_rows: int          # pure-model corpus (fallback + guards)
    residual_rows: int          # residual-model corpus (0 = fallback-only run)
    mae: float                  # pure-model in-sample MAE (continuity with v1.0 reporting)
    residual_std: float         # pure-model residual std
    residual_model_std: float | None  # correction-model residual std (None if skipped)


def predict_market_prior(training: pd.DataFrame,
                         current_week: pd.DataFrame,
                         fbs_scope: bool) -> V11Result:
    """v1.1: for priced games, predicted_margin = -Spread + correction,
    where the correction model is trained to predict the RESIDUAL vs
    the line. Unpriced games use the v1.0 pure-stats model and emit SU
    only (the pipeline drops their ATS output downstream).

    fbs_scope=True (NCAAFB) trains the correction on the FBS∩priced
    intersection per the design; False (NFL) trains on all priced games.
    """
    # The pure model always fits: it serves unpriced slate rows and is
    # the whole-slate fallback when the residual corpus is too thin.
    su = predict_straight_up(training, current_week)
    predictions = predict_ats(su)
    predictions["ModelPath"] = "fallback"

    decided = training[training["Winner"].isin(["HOME", "AWAY"])].copy()
    residual_corpus = decided[is_priced(decided)]
    if fbs_scope and "FbsParticipant" in residual_corpus.columns:
        # psql CSV delivers booleans as 't'/'f' strings.
        fbs = residual_corpus["FbsParticipant"].astype(str).str.lower().isin(["t", "true", "1"])
        residual_corpus = residual_corpus[fbs]

    if len(residual_corpus) < MIN_RESIDUAL_ROWS:
        # Not enough priced history (early-2022 backtests): honest
        # fallback for everything rather than a fragile fit.
        return V11Result(
            predictions=predictions,
            training_rows=su.training_rows,
            residual_rows=0,
            mae=su.mae,
            residual_std=su.residual_std,
            residual_model_std=None,
        )

    residual_corpus = residual_corpus.copy()

    # The walk-forward error estimate below depends on chronological
    # order. The extraction SQL orders by (SeasonYear, WeekNumber);
    # enforce it here too so the contract doesn't rest on SQL ordering
    # surviving the pandas round-trip. Stable sort preserves within-week
    # extraction order.
    if {"SeasonYear", "WeekNumber"}.issubset(residual_corpus.columns):
        residual_corpus = residual_corpus.sort_values(
            ["SeasonYear", "WeekNumber"], kind="stable")

    # r = actual_margin - market_prediction = margin - (-Spread)
    residual_corpus["Residual"] = (
        residual_corpus["HomeScore"] - residual_corpus["AwayScore"]
        + residual_corpus["Spread"])

    x_train = residual_corpus[FEATURE_COLS].fillna(0)
    y_train = residual_corpus["Residual"]
    correction = LinearRegression()
    correction.fit(x_train, y_train)

    # v1.1.1: the probability scale must be the HONEST out-of-sample
    # error, not the in-sample fit std — overfit shrinks the latter well
    # below reality (observed: ~13 in-sample vs ~16-17 true on the 2025
    # sweep), which saturates every probability away from 0.5 and made
    # the v1.1.0 low buckets wildly overconfident (pred 4.5% -> actual
    # 25.6%) while ATS Brier landed WORSE than always-saying-50%.
    # WALK-FORWARD, not KFold: unshuffled KFold validates each
    # chronological block with a model trained partly on FUTURE games —
    # temporal leakage that biases the estimate optimistic, the same
    # defect family in miniature. Deployment predicts forward; the error
    # estimate does too: each block is predicted by a model trained only
    # on the rows before it. Deterministic.
    residual_model_std = _walk_forward_residual_std(x_train, y_train)
    if not np.isfinite(residual_model_std) or residual_model_std <= 0:
        raise ModelError(
            f"Correction-model residual std is {residual_model_std} — degenerate fit.")

    priced_mask = is_priced(predictions)
    if priced_mask.any():
        priced = predictions[priced_mask]
        corr = correction.predict(priced[FEATURE_COLS].fillna(0))

        # Margin: the market's opinion plus our measured disagreement.
        margin = -priced["Spread"].to_numpy() + corr
        predictions.loc[priced_mask, "PredictedMargin"] = margin
        predictions.loc[priced_mask, "WinProbability"] = norm.sf(
            0, loc=margin, scale=residual_model_std).clip(0.01, 0.99)

        # Cover: margin + Spread = correction — the cover probability IS
        # the disagreement. A correction near zero yields ~50% at low
        # confidence, the truthful ATS output.
        predictions.loc[priced_mask, "HomeCoverProbability"] = norm.sf(
            0, loc=corr, scale=residual_model_std).clip(0.01, 0.99)
        predictions.loc[priced_mask, "AwayCoverProbability"] = norm.sf(
            0, loc=-corr, scale=residual_model_std).clip(0.01, 0.99)
        predictions.loc[priced_mask, "AtsPredictedLabel"] = (
            predictions.loc[priced_mask, "HomeCoverProbability"]
            > predictions.loc[priced_mask, "AwayCoverProbability"]).astype(int)
        predictions.loc[priced_mask, "ModelPath"] = "residual"

    return V11Result(
        predictions=predictions,
        training_rows=su.training_rows,
        residual_rows=len(residual_corpus),
        mae=su.mae,
        residual_std=su.residual_std,
        residual_model_std=residual_model_std,
    )


WALK_FORWARD_FOLDS = 5


def _walk_forward_residual_std(x_train: pd.DataFrame, y_train: pd.Series) -> float:
    """Forward-only out-of-sample residual std: split chronologically into
    WALK_FORWARD_FOLDS blocks; each block (after the first) is predicted
    by a model trained on ALL preceding rows. Blocks whose preceding
    training slice is underdetermined (<= feature count) are skipped —
    at MIN_RESIDUAL_ROWS the later folds always qualify."""
    n = len(x_train)
    fold = n // WALK_FORWARD_FOLDS
    x_values = x_train.to_numpy()
    y_values = y_train.to_numpy()

    residuals: list[np.ndarray] = []
    for i in range(1, WALK_FORWARD_FOLDS):
        train_end = fold * i
        test_end = fold * (i + 1) if i < WALK_FORWARD_FOLDS - 1 else n
        if train_end <= len(FEATURE_COLS):
            continue
        model = LinearRegression().fit(x_values[:train_end], y_values[:train_end])
        residuals.append(
            y_values[train_end:test_end] - model.predict(x_values[train_end:test_end]))

    if not residuals:
        raise ModelError(
            f"Walk-forward validation produced no usable folds from {n} rows — "
            f"corpus too thin for an honest error estimate.")

    return float(np.std(np.concatenate(residuals)))
