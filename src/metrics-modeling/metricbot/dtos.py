"""Prediction DTO assembly — ported from generate_contest_prediction_dtos.py.

Both prediction types are expressed HOME-team-relative (WinnerFranchiseSeasonId
is always the home team; the probability is the home team's) for
consistency with the deetsMeter UI's rendering convention.
"""

from __future__ import annotations

import pandas as pd

from .model import is_priced

PICKTYPE_STRAIGHT_UP = 1
PICKTYPE_ATS = 2


def build_prediction_dtos(predictions: pd.DataFrame) -> list[dict]:
    """SU for every contest; ATS only for priced contests (is_priced —
    the v1.1 canonical predicate). Unpriced rows previously produced
    ATS DTOs with NaN probabilities — no line, no pick."""
    records: list[dict] = []

    for _, row in predictions.iterrows():
        records.append({
            "ContestId": row["ContestId"],
            "WinnerFranchiseSeasonId": row["HomeFranchiseSeasonId"],
            "WinProbability": round(float(row["WinProbability"]), 4),
            "PredictionType": PICKTYPE_STRAIGHT_UP,
            "ModelVersion": row["ModelVersion"],
        })

    for _, row in predictions[is_priced(predictions)].iterrows():
        records.append({
            "ContestId": row["ContestId"],
            "WinnerFranchiseSeasonId": row["HomeFranchiseSeasonId"],
            "WinProbability": round(float(row["HomeCoverProbability"]), 4),
            "PredictionType": PICKTYPE_ATS,
            "ModelVersion": row["ModelVersion"],
        })

    return records
