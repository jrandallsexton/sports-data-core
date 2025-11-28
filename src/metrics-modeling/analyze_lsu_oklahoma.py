import pandas as pd
import json

# Load data
df_su = pd.read_csv('./data/predictions_straightup_raw.csv')
df_week = pd.read_csv('./data/competition_metrics_week_14.csv')
df_full = pd.read_csv('./data/competition_metrics_full.csv')

# Game info
contest_id = '07e405d9-bd4c-a061-36fd-ba1cc0199042'
oklahoma_id = 'df05dea8-6ff8-2206-95d0-78d40fbe1d6f'
lsu_id = 'c13b7c74-6892-3efa-2492-36ebf5220464'

# Get prediction
su_row = df_su[df_su['ContestId'] == contest_id].iloc[0]
week_row = df_week[df_week['ContestId'] == contest_id].iloc[0]

print("="*80)
print("LSU @ OKLAHOMA ANALYSIS")
print("="*80)

print(f"\nContest ID: {contest_id}")
print(f"Home Team ID: {su_row['HomeFranchiseSeasonId']}")
print(f"Away Team ID: {su_row['AwayFranchiseSeasonId']}")

is_okla_home = su_row['HomeFranchiseSeasonId'] == oklahoma_id
print(f"\nOklahoma is: {'HOME' if is_okla_home else 'AWAY'}")
print(f"LSU is: {'AWAY' if is_okla_home else 'HOME'}")

print(f"\n{'='*80}")
print("PREDICTION:")
print(f"{'='*80}")
print(f"Predicted Margin: {su_row['PredictedMargin']:+.2f} points")
print(f"Home Win Probability: {su_row['WinProbability']:.1%}")
print(f"Away Win Probability: {1 - su_row['WinProbability']:.1%}")
print(f"Spread: {week_row['Spread']:+.1f}")

if is_okla_home:
    okla_prob = su_row['WinProbability']
    lsu_prob = 1 - su_row['WinProbability']
else:
    okla_prob = 1 - su_row['WinProbability']
    lsu_prob = su_row['WinProbability']

print(f"\n🏈 Oklahoma (#8 ranked, 9-2): {okla_prob:.1%} to win")
print(f"🏈 LSU (7-4): {lsu_prob:.1%} to win")

# Get the actual feature values used
print(f"\n{'='*80}")
print("FEATURE VALUES USED BY MODEL:")
print(f"{'='*80}")

# Key offensive metrics
print("\nOFFENSIVE METRICS (Higher is better):")
print(f"                           {'Home':>10}  {'Away':>10}")
print(f"Yards Per Play:            {week_row['HomeYpp']:>10.2f}  {week_row['AwayYpp']:>10.2f}")
print(f"Success Rate:              {week_row['HomeSuccessRate']:>10.1%}  {week_row['AwaySuccessRate']:>10.1%}")
print(f"Explosive Rate:            {week_row['HomeExplosiveRate']:>10.1%}  {week_row['AwayExplosiveRate']:>10.1%}")
print(f"Points Per Drive:          {week_row['HomePointsPerDrive']:>10.2f}  {week_row['AwayPointsPerDrive']:>10.2f}")
print(f"3rd/4th Down Rate:         {week_row['HomeThirdFourthRate']:>10.1%}  {week_row['AwayThirdFourthRate']:>10.1%}")
print(f"RedZone TD Rate:           {week_row['HomeRzTdRate']:>10.1%}  {week_row['AwayRzTdRate']:>10.1%}")

# Key defensive metrics (opponent stats - lower is better)
print("\nDEFENSIVE METRICS (Lower opponent stats is better defense):")
print(f"                           {'Home':>10}  {'Away':>10}")
print(f"Opp Yards Per Play:        {week_row['HomeOppYpp']:>10.2f}  {week_row['AwayOppYpp']:>10.2f}")
print(f"Opp Success Rate:          {week_row['HomeOppSuccessRate']:>10.1%}  {week_row['AwayOppSuccessRate']:>10.1%}")
print(f"Opp Explosive Rate:        {week_row['HomeOppExplosiveRate']:>10.1%}  {week_row['AwayOppExplosiveRate']:>10.1%}")
print(f"Opp Points Per Drive:      {week_row['HomeOppPointsPerDrive']:>10.2f}  {week_row['AwayOppPointsPerDrive']:>10.2f}")

# Other factors
print("\nOTHER FACTORS:")
print(f"                           {'Home':>10}  {'Away':>10}")
print(f"Time Possession Ratio:     {week_row['HomeTimePossRatio']:>10.1%}  {week_row['AwayTimePossRatio']:>10.1%}")
print(f"Turnover Margin/Drive:     {week_row['HomeTurnoverMarginPerDrive']:>10.3f}  {week_row['AwayTurnoverMarginPerDrive']:>10.3f}")
print(f"Field Position Diff:       {week_row['HomeFieldPosDiff']:>10.2f}  {week_row['AwayFieldPosDiff']:>10.2f}")

print(f"\n{'='*80}")
print("ANALYSIS:")
print(f"{'='*80}")

if is_okla_home:
    okla_metrics = {
        'Ypp': week_row['HomeYpp'],
        'SuccessRate': week_row['HomeSuccessRate'],
        'PPD': week_row['HomePointsPerDrive'],
        'OppYpp': week_row['HomeOppYpp'],
        'OppPPD': week_row['HomeOppPointsPerDrive']
    }
    lsu_metrics = {
        'Ypp': week_row['AwayYpp'],
        'SuccessRate': week_row['AwaySuccessRate'],
        'PPD': week_row['AwayPointsPerDrive'],
        'OppYpp': week_row['AwayOppYpp'],
        'OppPPD': week_row['AwayOppPointsPerDrive']
    }
else:
    okla_metrics = {
        'Ypp': week_row['AwayYpp'],
        'SuccessRate': week_row['AwaySuccessRate'],
        'PPD': week_row['AwayPointsPerDrive'],
        'OppYpp': week_row['AwayOppYpp'],
        'OppPPD': week_row['AwayOppPointsPerDrive']
    }
    lsu_metrics = {
        'Ypp': week_row['HomeYpp'],
        'SuccessRate': week_row['HomeSuccessRate'],
        'PPD': week_row['HomePointsPerDrive'],
        'OppYpp': week_row['HomeOppYpp'],
        'OppPPD': week_row['HomeOppPointsPerDrive']
    }

print("\nOklahoma vs LSU metric comparison:")
for metric in okla_metrics:
    okla_val = okla_metrics[metric]
    lsu_val = lsu_metrics[metric]
    
    # Lower is better for defensive (Opp) stats
    if metric.startswith('Opp'):
        better = "Oklahoma" if okla_val < lsu_val else "LSU"
        diff_pct = ((lsu_val - okla_val) / okla_val * 100) if okla_val != 0 else 0
    else:
        better = "Oklahoma" if okla_val > lsu_val else "LSU"
        diff_pct = ((okla_val - lsu_val) / lsu_val * 100) if lsu_val != 0 else 0
    
    print(f"  {metric:15s}: Okla {okla_val:7.3f} vs LSU {lsu_val:7.3f} → {better:8s} better by {abs(diff_pct):5.1f}%")

print(f"\n⚠️  MODEL ISSUE: The model predicts LSU {lsu_prob:.1%} despite Oklahoma")
print(f"    appearing superior in most metrics. This suggests:")
print(f"    1. Feature values may not accurately represent team quality")
print(f"    2. Model may not weight important features correctly")
print(f"    3. Missing contextual features (rankings, strength of schedule, etc.)")
print(f"    4. Data quality issues in the metrics")
