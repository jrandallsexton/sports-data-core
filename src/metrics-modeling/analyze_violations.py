import json

# Load the JSON
with open('./data/contest_predictions.json') as f:
    data = json.load(f)

# Group by ContestId
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
                violations.append({
                    'contest': cid,
                    'su': preds['SU']['prob'],
                    'ats': preds['ATS']['prob'],
                    'diff': preds['ATS']['prob'] - preds['SU']['prob']
                })

# Sort by difference
violations.sort(key=lambda x: x['diff'], reverse=True)

print(f'Total contests: {len(contests)}')
print(f'Violations (ATS > SU for same team): {len(violations)}')
print()
print('Top 10 violations:')
for v in violations[:10]:
    print(f"Contest: {v['contest'][:8]}... | SU: {v['su']:.4f} | ATS: {v['ats']:.4f} | Diff: +{v['diff']:.4f}")
