# predict_straightup.py
import pandas as pd
import numpy as np
from sklearn.linear_model import LinearRegression
from scipy.stats import norm

# === Load training and prediction data ===
df_train = pd.read_csv("./data/competition_metrics_full.csv")
df_predict = pd.read_csv("./data/competition_metrics_week_1.csv")

# === Filter training data and calculate margin ===
df_train = df_train[df_train["Winner"].isin(["HOME", "AWAY"])].copy()
df_train["Margin"] = df_train["HomeScore"] - df_train["AwayScore"]

# === Define model input features (NO spread - pure performance metrics) ===
feature_cols = [
    'HomeYpp', 'HomeSuccessRate', 'HomeExplosiveRate', 'HomePointsPerDrive',
    'HomeThirdFourthRate', 'HomeRzTdRate', 'HomeRzScoreRate', 'HomeTimePossRatio',
    'HomeOppYpp', 'HomeOppSuccessRate', 'HomeOppExplosiveRate', 'HomeOppPointsPerDrive',
    'HomeOppThirdFourthRate', 'HomeOppRzTdRate', 'HomeOppScoreTdRate',
    'HomeNetPunt', 'HomeFgPctShrunk', 'HomeFieldPosDiff', 'HomeTurnoverMarginPerDrive',
    'HomePenaltyYardsPerPlay',
    # Home scoring and margin metrics
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
    # Away scoring and margin metrics
    'AwayPtsScoredAvg', 'AwayPtsScoredMin', 'AwayPtsScoredMax',
    'AwayPtsAllowedAvg', 'AwayPtsAllowedMin', 'AwayPtsAllowedMax',
    'AwayMarginWinAvg', 'AwayMarginWinMin', 'AwayMarginWinMax',
    'AwayMarginLossAvg', 'AwayMarginLossMin', 'AwayMarginLossMax'
]

# === Train regression model to predict margin ===
X_train = df_train[feature_cols].fillna(0)
y_train = df_train["Margin"]

model = LinearRegression()
model.fit(X_train, y_train)

# Calculate residual standard deviation
y_train_pred = model.predict(X_train)
residuals = y_train - y_train_pred
residual_std = np.std(residuals)

print(f"📊 SU Model: MAE = {np.mean(np.abs(residuals)):.2f} points, Residual Std = {residual_std:.2f} points")

# === Predict current week ===
X_predict = df_predict[feature_cols].fillna(0)
df_predict["PredictedMargin"] = model.predict(X_predict)

# Calculate P(Home wins) = P(Margin > 0)
df_predict["WinProbability"] = norm.sf(0, loc=df_predict["PredictedMargin"], scale=residual_std)

# Clip to reasonable range
df_predict["WinProbability"] = df_predict["WinProbability"].clip(0.01, 0.99)

# Determine predicted label
df_predict["PredictedLabel"] = (df_predict["WinProbability"] > 0.5).astype(int)

df_predict["ModelVersion"] = "MetricBot-v1.0.0"
df_predict["ResidualStd"] = residual_std  # Save for ATS model

# === Save raw predictions ===
output_cols = [
    "ContestId",
    "HomeFranchiseSeasonId",
    "AwayFranchiseSeasonId",
    "PredictedLabel",
    "PredictedMargin",
    "WinProbability",
    "ResidualStd",
    "ModelVersion"
]
df_predict[output_cols].to_csv("./data/predictions_straightup_raw.csv", index=False)

print("✅ Straight-up raw predictions written to ./data/predictions_straightup_raw.csv")

















