# predict_ats.py
import pandas as pd
from sklearn.linear_model import LogisticRegression

# === Load data ===
df_train = pd.read_csv("./data/competition_metrics_full.csv")
df_predict = pd.read_csv("./data/competition_metrics_week_11.csv")

# === Derive ATS label (exclude nulls or ties against spread) ===
df_train = df_train.dropna(subset=["HomeScore", "AwayScore", "Spread"]).copy()
df_train["Margin"] = df_train["HomeScore"] - df_train["AwayScore"]
df_train["CoveredSpread"] = (df_train["Margin"] > df_train["Spread"]).astype(int)

# === Filter out pushes (margin == spread) ===
df_train = df_train[df_train["Margin"] != df_train["Spread"]].copy()

# === Features ===
feature_cols = [
    'HomeYpp', 'HomeSuccessRate', 'HomeExplosiveRate', 'HomePointsPerDrive',
    'HomeThirdFourthRate', 'HomeRzTdRate', 'HomeRzScoreRate', 'HomeTimePossRatio',
    'HomeOppYpp', 'HomeOppSuccessRate', 'HomeOppExplosiveRate', 'HomeOppPointsPerDrive',
    'HomeOppThirdFourthRate', 'HomeOppRzTdRate', 'HomeOppScoreTdRate',
    'HomeNetPunt', 'HomeFgPctShrunk', 'HomeFieldPosDiff', 'HomeTurnoverMarginPerDrive',
    'HomePenaltyYardsPerPlay',

    'AwayYpp', 'AwaySuccessRate', 'AwayExplosiveRate', 'AwayPointsPerDrive',
    'AwayThirdFourthRate', 'AwayRzTdRate', 'AwayRzScoreRate', 'AwayTimePossRatio',
    'AwayOppYpp', 'AwayOppSuccessRate', 'AwayOppExplosiveRate', 'AwayOppPointsPerDrive',
    'AwayOppThirdFourthRate', 'AwayOppRzTdRate', 'AwayOppScoreTdRate',
    'AwayNetPunt', 'AwayFgPctShrunk', 'AwayFieldPosDiff', 'AwayTurnoverMarginPerDrive',
    'AwayPenaltyYardsPerPlay',

    'Spread'
]

# === Train model ===
X_train = df_train[feature_cols].fillna(0)
y_train = df_train["CoveredSpread"]

model = LogisticRegression(max_iter=1000)
model.fit(X_train, y_train)

# === Predict ===
X_predict = df_predict[feature_cols].fillna(0)
df_predict["PredictedLabel"] = model.predict(X_predict)
df_predict["HomeCoverProbability"] = model.predict_proba(X_predict)[:, 1]  # P(home covers)
df_predict["ModelVersion"] = "MetricBot-v1.0.0"

# === Save raw predictions ===
raw_output_cols = [
    "ContestId",
    "HomeFranchiseSeasonId",
    "AwayFranchiseSeasonId",
    "PredictedLabel",
    "HomeCoverProbability",
    "ModelVersion"
]
df_predict[raw_output_cols].to_csv("./data/predictions_ats_raw.csv", index=False)

print("✅ Raw ATS predictions written to ./data/predictions_ats_raw.csv")
