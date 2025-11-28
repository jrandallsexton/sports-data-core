import pandas as pd
import json
from scipy.stats import norm

# Load predictions
with open('./data/contest_predictions.json') as f:
    data = json.load(f)

# Get a positive spread violation
violation_cid = 'fd115411-4a48-8430-42a4-e27601abf291'

# Get SU and ATS predictions
for pred in data:
    if pred['ContestId'] == violation_cid:
        print(f"{'='*60}")
        print(f"Type: {'SU' if pred['PredictionType'] == 1 else 'ATS'}")
        print(f"Winner ID: {pred['WinnerFranchiseSeasonId'][:8]}...")
        print(f"Probability: {pred['WinProbability']:.4f}")
        print()

# Get raw data
df_su = pd.read_csv('./data/predictions_straightup_raw.csv')
df_ats = pd.read_csv('./data/predictions_ats_raw.csv')
df_week = pd.read_csv('./data/competition_metrics_week_14.csv')

su_row = df_su[df_su['ContestId'] == violation_cid].iloc[0]
ats_row = df_ats[df_ats['ContestId'] == violation_cid].iloc[0]
week_row = df_week[df_week['ContestId'] == violation_cid].iloc[0]

print(f"{'='*60}")
print("RAW DATA:")
print(f"Spread: {week_row['Spread']:.1f} (positive = home underdog)")
print(f"PredictedMargin: {su_row['PredictedMargin']:.2f}")
print(f"ResidualStd: {su_row['ResidualStd']:.2f}")
print()

print(f"{'='*60}")
print("CALCULATED VALUES:")
margin = su_row['PredictedMargin']
spread = week_row['Spread']
std = su_row['ResidualStd']

print(f"MarginVsSpread = {margin:.2f} - {spread:.1f} = {margin - spread:.2f}")
print()

print("P(Home wins) = P(Margin > 0):")
p_home_win = norm.sf(0, loc=margin, scale=std)
print(f"  = norm.sf(0, loc={margin:.2f}, scale={std:.2f})")
print(f"  = {p_home_win:.4f}")
print()

print(f"P(Home covers) = P(Margin > {spread:.1f}):")
margin_vs_spread = margin - spread
p_home_cover = norm.sf(0, loc=margin_vs_spread, scale=std)
print(f"  = norm.sf(0, loc={margin_vs_spread:.2f}, scale={std:.2f})")
print(f"  = {p_home_cover:.4f}")
print()

print(f"{'='*60}")
print("VALIDATION:")
print(f"Spread is POSITIVE ({spread:.1f}) → Home is UNDERDOG")
print(f"MarginVsSpread ({margin_vs_spread:.2f}) < Margin ({margin:.2f})")
print(f"Therefore P(Margin > {spread:.1f}) < P(Margin > 0) MUST be true")
print(f"Expected: P(cover) < P(win)")
print(f"Actual: {p_home_cover:.4f} {'<' if p_home_cover < p_home_win else '>'} {p_home_win:.4f}")
print(f"{'CORRECT' if p_home_cover < p_home_win else 'VIOLATION!'}")
