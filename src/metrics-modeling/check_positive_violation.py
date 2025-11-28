import pandas as pd
import json

# Load predictions
with open('./data/contest_predictions.json') as f:
    data = json.load(f)

# Find the contest with positive spread violation
violation_cid = '3b5868a5-22d7-91b1-8d9f-177e2d4513fe'

# Get SU and ATS predictions for this contest
for pred in data:
    if pred['ContestId'] == violation_cid:
        print(f"Type: {'SU' if pred['PredictionType'] == 1 else 'ATS'}")
        print(f"Winner: {pred['WinnerFranchiseSeasonId'][:8]}...")
        print(f"Probability: {pred['WinProbability']:.4f}")
        print()

# Get raw predictions
df_su = pd.read_csv('./data/predictions_straightup_raw.csv')
df_ats = pd.read_csv('./data/predictions_ats_raw.csv')
df_week = pd.read_csv('./data/competition_metrics_week_14.csv')

# Get details
su_row = df_su[df_su['ContestId'] == violation_cid].iloc[0]
ats_row = df_ats[df_ats['ContestId'] == violation_cid].iloc[0]
week_row = df_week[df_week['ContestId'] == violation_cid].iloc[0]

print("Raw data:")
print(f"PredictedMargin: {su_row['PredictedMargin']:.2f}")
print(f"Spread: {week_row['Spread']:.2f}")
print(f"MarginVsSpread: {ats_row['MarginVsSpread']:.2f}")
print()
print(f"P(Home wins) = P(Margin > 0): {su_row['WinProbability']:.4f}")
print(f"P(Home covers) = P(Margin > {week_row['Spread']:.1f}): {ats_row['HomeCoverProbability']:.4f}")
print()
print("Home team:", week_row['HomeAbbreviation'])
print("Away team:", week_row['AwayAbbreviation'])
