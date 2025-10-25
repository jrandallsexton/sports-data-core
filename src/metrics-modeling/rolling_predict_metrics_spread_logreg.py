import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score
from sklearn.preprocessing import LabelEncoder

# --- Load the CSV ---
df = pd.read_csv("./data/competition_metrics.csv")

# --- Convert winner to binary: HOME = 1, AWAY = 0 ---
df = df[df["Winner"].isin(["HOME", "AWAY"])]  # exclude ties
df["Winner"] = df["Winner"].map({"HOME": 1, "AWAY": 0})

# --- Define feature columns ---
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

# --- Rolling prediction week-by-week ---
week_numbers = sorted(df["WeekNumber"].unique())
results = []

for i in range(1, len(week_numbers)):
    train_weeks = week_numbers[:i]
    test_week = week_numbers[i]

    train_df = df[df["WeekNumber"].isin(train_weeks)]
    test_df = df[df["WeekNumber"] == test_week]

    # Basic sanity check
    if train_df.empty or test_df.empty:
        continue

    X_train = train_df[feature_cols].fillna(0)
    y_train = train_df["Winner"]

    X_test = test_df[feature_cols].fillna(0)
    y_test = test_df["Winner"]

    model = LogisticRegression(max_iter=1000)
    model.fit(X_train, y_train)

    y_pred = model.predict(X_test)
    accuracy = accuracy_score(y_test, y_pred)
    results.append((test_week, accuracy, len(test_df)))

# --- Report results ---
print("\n📊 Rolling Accuracy by Week")
overall_correct = 0
overall_total = 0

for week, acc, total in results:
    correct = round(acc * total)
    print(f"Week {week}: {acc * 100:.2f}% accuracy on {total} games")
    overall_correct += correct
    overall_total += total

overall_accuracy = overall_correct / overall_total
print(f"\n✅ Overall rolling accuracy: {overall_accuracy * 100:.2f}% across {overall_total} games")
