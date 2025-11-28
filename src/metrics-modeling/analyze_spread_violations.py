import pandas as pd
import json

# Load predictions
with open('./data/contest_predictions.json') as f:
    data = json.load(f)

# Group by contest
contests = {}
for pred in data:
    cid = pred['ContestId']
    if cid not in contests:
        contests[cid] = {}
    if pred['PredictionType'] == 1:
        contests[cid]['SU'] = {'winner': pred['WinnerFranchiseSeasonId'], 'prob': pred['WinProbability']}
    else:
        contests[cid]['ATS'] = {'winner': pred['WinnerFranchiseSeasonId'], 'prob': pred['WinProbability']}

# Find violations
violations = []
for cid, preds in contests.items():
    if 'SU' in preds and 'ATS' in preds:
        if preds['SU']['winner'] == preds['ATS']['winner']:
            if preds['ATS']['prob'] > preds['SU']['prob']:
                violations.append(cid)

# Load spreads
df_week = pd.read_csv('./data/competition_metrics_week_14.csv')
df_violations = df_week[df_week['ContestId'].isin(violations)][['ContestId', 'Spread']]

neg_spread = (df_violations['Spread'] < 0).sum()
pos_spread = (df_violations['Spread'] > 0).sum()

print(f'Total violations: {len(violations)}')
print(f'Negative spread (home favored): {neg_spread}')
print(f'Positive spread (home underdog): {pos_spread}')
print()
print('Sample violations with spreads:')
print(df_violations.head(10).to_string(index=False))
