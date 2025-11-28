import pandas as pd

df = pd.read_csv('./data/competition_metrics_week_14.csv')
df_su = pd.read_csv('./data/predictions_straightup_raw.csv')

row_week = df.iloc[1]
row_su = df_su.iloc[1]

print(f"Contest 2:")
print(f"Spread: {row_week['Spread']}")
print(f"Predicted Margin: {row_su['PredictedMargin']:.2f}")
print(f"Home ID: {row_week['HomeFranchiseSeasonId'][:20]}...")
print(f"Away ID: {row_week['AwayFranchiseSeasonId'][:20]}...")
