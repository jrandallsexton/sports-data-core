import pandas as pd
import json
from scipy.stats import norm

# Load data
df_su = pd.read_csv('./data/predictions_straightup_raw.csv')
df_ats = pd.read_csv('./data/predictions_ats_raw.csv')
df_week = pd.read_csv('./data/competition_metrics_week_14.csv')

with open('./data/contest_predictions.json') as f:
    dtos = json.load(f)

# BYU contest
contest_id = '32d1426c-eb3b-966e-12c7-af0c3850cb0c'
byu_id = 'f612a671-718a-55cd-60f0-8fde6743e286'

# Get data
su_row = df_su[df_su['ContestId'] == contest_id].iloc[0]
ats_row = df_ats[df_ats['ContestId'] == contest_id].iloc[0]
week_row = df_week[df_week['ContestId'] == contest_id].iloc[0]

su_dto = [d for d in dtos if d['ContestId'] == contest_id and d['PredictionType'] == 1][0]
ats_dto = [d for d in dtos if d['ContestId'] == contest_id and d['PredictionType'] == 2][0]

print("="*80)
print("BYU GAME ANALYSIS")
print("="*80)
print(f"\nContest ID: {contest_id}")
print(f"BYU ID: {byu_id}")
print(f"\nHome Team ID: {su_row['HomeFranchiseSeasonId']}")
print(f"Away Team ID: {su_row['AwayFranchiseSeasonId']}")
print(f"\nIs BYU Home? {su_row['HomeFranchiseSeasonId'] == byu_id}")
print(f"Spread: {week_row['Spread']:.1f} (negative = home favored)")

print(f"\n{'='*80}")
print("RAW PREDICTIONS:")
print(f"{'='*80}")
print(f"\nPredicted Margin: {su_row['PredictedMargin']:.2f} (positive = home wins)")
print(f"Residual Std: {su_row['ResidualStd']:.2f}")

print(f"\nSU Probabilities:")
print(f"  Home Win: {su_row['WinProbability']:.4f} ({su_row['WinProbability']*100:.1f}%)")
print(f"  Away Win: {1 - su_row['WinProbability']:.4f} ({(1-su_row['WinProbability'])*100:.1f}%)")
print(f"  Predicted Winner: {'Home (BYU)' if su_row['PredictedLabel'] == 1 else 'Away'}")

print(f"\nATS Probabilities:")
print(f"  Home Cover: {ats_row['HomeCoverProbability']:.4f} ({ats_row['HomeCoverProbability']*100:.1f}%)")
print(f"  Away Cover: {ats_row['AwayCoverProbability']:.4f} ({ats_row['AwayCoverProbability']*100:.1f}%)")
print(f"  Predicted ATS Winner: {'Home (BYU)' if ats_row['PredictedLabel'] == 1 else 'Away'}")

print(f"\n{'='*80}")
print("DTOs (What goes to API):")
print(f"{'='*80}")
print(f"\nSU DTO:")
print(f"  Winner ID: {su_dto['WinnerFranchiseSeasonId']}")
print(f"  Is BYU? {su_dto['WinnerFranchiseSeasonId'] == byu_id}")
print(f"  Probability: {su_dto['WinProbability']:.4f} ({su_dto['WinProbability']*100:.1f}%)")

print(f"\nATS DTO:")
print(f"  Winner ID: {ats_dto['WinnerFranchiseSeasonId']}")
print(f"  Is BYU? {ats_dto['WinnerFranchiseSeasonId'] == byu_id}")
print(f"  Probability: {ats_dto['WinProbability']:.4f} ({ats_dto['WinProbability']*100:.1f}%)")

print(f"\n{'='*80}")
print("ANALYSIS:")
print(f"{'='*80}")

margin = su_row['PredictedMargin']
spread = week_row['Spread']
std = su_row['ResidualStd']

print(f"\nBYU is {-spread:.1f}-point favorite (Spread = {spread:.1f})")
print(f"Predicted margin: {margin:+.2f} (BYU expected to win by {margin:.1f})")
print(f"\nP(BYU wins) = P(Margin > 0) = {su_row['WinProbability']:.1%}")
print(f"P(BYU covers {-spread:.1f}) = P(Margin > {spread:.1f}) = {ats_row['HomeCoverProbability']:.1%}")

print(f"\n🔴 ISSUE: BYU is a huge favorite but only {su_row['WinProbability']:.1%} to win?")
print(f"         This suggests margin prediction is TOO SMALL for a {-spread:.1f}-point favorite")

# Manual calculation
print(f"\nManual calculation verification:")
print(f"P(Margin > 0) = norm.sf(0, loc={margin:.2f}, scale={std:.2f})")
p_win_calc = norm.sf(0, loc=margin, scale=std)
print(f"             = {p_win_calc:.4f} ✓ Matches")

print(f"\nP(Margin > {spread:.1f}) = norm.sf(0, loc={margin - spread:.2f}, scale={std:.2f})")
p_cover_calc = norm.sf(0, loc=margin - spread, scale=std)
print(f"                = {p_cover_calc:.4f} ✓ Matches")

print(f"\n{'='*80}")
print("ROOT CAUSE:")
print(f"{'='*80}")
print(f"The predicted margin ({margin:.2f}) is TOO SMALL for a {-spread:.1f}-point favorite.")
print(f"The model is predicting BYU wins by only {margin:.1f} points,")
print(f"but vegas (spread) says BYU should win by {-spread:.1f} points.")
print(f"\nWhen margin ({margin:.2f}) - spread ({spread:.1f}) = {margin - spread:.2f}")
print(f"This is MUCH MORE confident than just margin > 0")
print(f"Hence P(cover) > P(win)")
print(f"\nThis is MATHEMATICALLY CORRECT but indicates the margin prediction")
print(f"may not align well with betting lines.")
