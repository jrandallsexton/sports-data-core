# train_model.py
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import accuracy_score
import os

# === Load CSV ===
file_path = os.path.join("./data", "competition_metrics.csv")
df = pd.read_csv(file_path)

print("🚨 CSV Columns:")
print(df.columns.tolist())


# === Prepare Label ===
# Home win = 1, Away win = 0
df["Label"] = (df["HomeScore"] > df["AwayScore"]).astype(int)

# === Feature Columns (excluding target and ID columns) ===
feature_cols = [
    # Home team metrics
    'HomeYpp', 'HomeSuccessRate', 'HomeExplosiveRate', 'HomePointsPerDrive',
    'HomeThirdFourthRate', 'HomeRzTdRate', 'HomeRzScoreRate', 'HomeTimePossRatio',
    'HomeOppYpp', 'HomeOppSuccessRate', 'HomeOppExplosiveRate', 'HomeOppPointsPerDrive',
    'HomeOppThirdFourthRate', 'HomeOppRzTdRate', 'HomeOppScoreTdRate',
    'HomeNetPunt', 'HomeFgPctShrunk', 'HomeFieldPosDiff',
    'HomeTurnoverMarginPerDrive', 'HomePenaltyYardsPerPlay',

    # Away team metrics
    'AwayYpp', 'AwaySuccessRate', 'AwayExplosiveRate', 'AwayPointsPerDrive',
    'AwayThirdFourthRate', 'AwayRzTdRate', 'AwayRzScoreRate', 'AwayTimePossRatio',
    'AwayOppYpp', 'AwayOppSuccessRate', 'AwayOppExplosiveRate', 'AwayOppPointsPerDrive',
    'AwayOppThirdFourthRate', 'AwayOppRzTdRate', 'AwayOppScoreTdRate',
    'AwayNetPunt', 'AwayFgPctShrunk', 'AwayFieldPosDiff',
    'AwayTurnoverMarginPerDrive', 'AwayPenaltyYardsPerPlay',

    # Game context
    'Spread'
]

# === Track Weekly Accuracy ===
weekly_results = []

for week in range(4, df["WeekNumber"].max() + 1):
    # Training = all weeks before current week
    train_df = df[df["WeekNumber"] < week]
    test_df = df[df["WeekNumber"] == week]

    # Skip if not enough data
    if len(train_df) < 10 or len(test_df) == 0:
        continue

    X_train = train_df[feature_cols].fillna(0)
    y_train = train_df["Label"]

    X_test = test_df[feature_cols].fillna(0)
    y_test = test_df["Label"]

    # === Train model ===
    model = RandomForestClassifier(n_estimators=100, random_state=42)
    model.fit(X_train, y_train)

    # === Predict & Evaluate ===
    y_pred = model.predict(X_test)
    acc = accuracy_score(y_test, y_pred)

    weekly_results.append((week, len(test_df), acc))
    print(f"Week {week}: {acc:.2%} accuracy on {len(test_df)} games")

# === Overall Summary ===
avg_acc = sum(r[2] * r[1] for r in weekly_results) / sum(r[1] for r in weekly_results)
print(f"\n📊 Overall accuracy across weeks: {avg_acc:.2%}")
