# predict_ats.py
import pandas as pd
import numpy as np
from scipy.stats import norm

# === Load SU predictions (which contain margin predictions) ===
df_su = pd.read_csv("./data/predictions_straightup_raw.csv")
df_week = pd.read_csv("./data/competition_metrics_week_14.csv")

# Merge to get spread
df_predict = df_su.merge(
    df_week[['ContestId', 'Spread']],
    on='ContestId',
    how='left'
)

# Get residual std from SU model (should be same value)
residual_std = df_predict["ResidualStd"].iloc[0]

print(f"📊 Using SU model predictions with Residual Std = {residual_std:.2f} points")

# === Calculate ATS probabilities using SU margin predictions ===
# Standard sports betting spread convention:
#   Negative spread = home favored (e.g., -17.5 means home must win by >17.5)
#   Positive spread = home underdog (e.g., +6.5 means home gets 6.5 points)
#
# P(Home covers) = P(Margin + Spread > 0)
#   - If home favored (-17.5): P(Margin - 17.5 > 0) = P(win by >17.5)
#   - If home underdog (+6.5): P(Margin + 6.5 > 0) = P(lose by <6.5 or win)
df_predict["MarginVsSpread"] = df_predict["PredictedMargin"] + df_predict["Spread"]
df_predict["HomeCoverProbability"] = norm.sf(0, loc=df_predict["MarginVsSpread"], scale=residual_std)

# P(Away covers) = complement
df_predict["AwayCoverProbability"] = norm.sf(0, loc=-df_predict["MarginVsSpread"], scale=residual_std)

# Clip probabilities
df_predict["HomeCoverProbability"] = df_predict["HomeCoverProbability"].clip(0.01, 0.99)
df_predict["AwayCoverProbability"] = df_predict["AwayCoverProbability"].clip(0.01, 0.99)

# Determine predicted label
df_predict["ATS_PredictedLabel"] = (df_predict["HomeCoverProbability"] > df_predict["AwayCoverProbability"]).astype(int)

df_predict["ModelVersion"] = "MetricBot-v1.0.0"

# === Save raw ATS predictions ===
raw_output_cols = [
    "ContestId",
    "HomeFranchiseSeasonId",
    "AwayFranchiseSeasonId",
    "ATS_PredictedLabel",
    "PredictedMargin",
    "HomeCoverProbability",
    "AwayCoverProbability",
    "ModelVersion"
]

# Rename ATS_PredictedLabel to PredictedLabel for compatibility
df_output = df_predict[raw_output_cols].copy()
df_output = df_output.rename(columns={"ATS_PredictedLabel": "PredictedLabel"})

df_output.to_csv("./data/predictions_ats_raw.csv", index=False)

print("✅ Raw ATS predictions written to ./data/predictions_ats_raw.csv")













