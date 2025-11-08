import pandas as pd
from sklearn.linear_model import LogisticRegression

# === Load training and prediction data ===
df_train = pd.read_csv("./data/competition_metrics_full.csv")
df_predict = pd.read_csv("./data/competition_metrics_week_11.csv")

# === Filter & label training data ===
df_train = df_train[df_train["Winner"].isin(["HOME", "AWAY"])].copy()
df_train["Label"] = df_train["Winner"].map({"HOME": 1, "AWAY": 0})

# === Define model input features ===
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
y_train = df_train["Label"]

model = LogisticRegression(max_iter=1000)
model.fit(X_train, y_train)

# === Predict current week ===
X_predict = df_predict[feature_cols].fillna(0)

df_predict["PredictedLabel"] = model.predict(X_predict)
df_predict["WinProbability"] = model.predict_proba(X_predict)[:, 1]  # probability that HOME wins
df_predict["ModelVersion"] = "MetricBot-v1.0.0"

# === Save raw predictions ===
output_cols = [
    "ContestId",
    "HomeFranchiseSeasonId",
    "AwayFranchiseSeasonId",
    "PredictedLabel",
    "WinProbability",
    "ModelVersion"
]

df_predict[output_cols].to_csv("./data/predictions_straightup_raw.csv", index=False)

print("✅ Straight-up predictions written to ./data/predictions_straightup_raw.csv")
