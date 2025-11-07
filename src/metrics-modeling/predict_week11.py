import pandas as pd
from sklearn.linear_model import LogisticRegression

# === Load combined dataset ===
df = pd.read_csv("./data/competition_metrics_full.csv")

# Filter for valid training data (HOME or AWAY wins only)
df = df[df["Winner"].isin(["HOME", "AWAY"]) | (df["WeekNumber"] == 11)]

# Map labels for supervised training
df["Label"] = df["Winner"].map({"HOME": 1, "AWAY": 0})

# === Clean & prep ===

# Map "Winner" to binary label
df["Label"] = df["Winner"].map({"HOME": 1, "AWAY": 0})

# Define feature columns (same as model training)
feature_cols = [
    'HomeYpp', 'HomeSuccessRate', 'HomeExplosiveRate', 'HomePointsPerDrive',
    'HomeThirdFourthRate', 'HomeRzTdRate', 'HomeRzScoreRate', 'HomeTimePossRatio',
    'HomeOppYpp', 'HomeOppSuccessRate', 'HomeOppExplosiveRate', 'HomeOppPointsPerDrive',
    'HomeOppThirdFourthRate', 'HomeOppRzTdRate', 'HomeOppScoreTdRate',
    'HomeNetPunt', 'HomeFgPctShrunk', 'HomeFieldPosDiff',
    'HomeTurnoverMarginPerDrive', 'HomePenaltyYardsPerPlay',

    'AwayYpp', 'AwaySuccessRate', 'AwayExplosiveRate', 'AwayPointsPerDrive',
    'AwayThirdFourthRate', 'AwayRzTdRate', 'AwayRzScoreRate', 'AwayTimePossRatio',
    'AwayOppYpp', 'AwayOppSuccessRate', 'AwayOppExplosiveRate', 'AwayOppPointsPerDrive',
    'AwayOppThirdFourthRate', 'AwayOppRzTdRate', 'AwayOppScoreTdRate',
    'AwayNetPunt', 'AwayFgPctShrunk', 'AwayFieldPosDiff',
    'AwayTurnoverMarginPerDrive', 'AwayPenaltyYardsPerPlay',

    'Spread'
]

# === Split into train and predict sets ===
train_df = df[df["WeekNumber"] < 11].copy()
predict_df = df[df["WeekNumber"] == 11].copy()

X_train = train_df[feature_cols].fillna(0)
y_train = train_df["Label"]

X_predict = predict_df[feature_cols].fillna(0)

# === Train model ===
model = LogisticRegression(max_iter=1000)
model.fit(X_train, y_train)

# === Predict Week 11 ===
predict_df["PredictedLabel"] = model.predict(X_predict)
predict_df["HomeWinProb"] = model.predict_proba(X_predict)[:, 1]
predict_df["AwayWinProb"] = model.predict_proba(X_predict)[:, 0]
predict_df["PredictedWinner"] = predict_df["PredictedLabel"].map({1: "HOME", 0: "AWAY"})

# === Save output ===
predict_df.to_csv("./data/week11_predictions.csv", index=False)
print("✅ Predictions saved to ./data/week11_predictions.csv")
