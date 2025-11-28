import json

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

# Find identical probabilities
identical = []
different = []
for cid, preds in contests.items():
    if 'SU' in preds and 'ATS' in preds:
        if preds['SU']['winner'] == preds['ATS']['winner']:
            if abs(preds['SU']['prob'] - preds['ATS']['prob']) < 0.0001:
                identical.append({
                    'contest': cid,
                    'su': preds['SU']['prob'],
                    'ats': preds['ATS']['prob']
                })
            else:
                different.append({
                    'contest': cid,
                    'su': preds['SU']['prob'],
                    'ats': preds['ATS']['prob'],
                    'diff': abs(preds['SU']['prob'] - preds['ATS']['prob'])
                })

print(f'Total contests: {len(contests)}')
print(f'Same winner, identical prob: {len(identical)}')
print(f'Same winner, different prob: {len(different)}')
print()
if len(identical) > 0:
    print('First 5 identical:')
    for v in identical[:5]:
        print(f"  Contest: {v['contest'][:8]}... | SU: {v['su']:.4f} | ATS: {v['ats']:.4f}")
print()
if len(different) > 0:
    print('First 5 different:')
    for v in different[:5]:
        print(f"  Contest: {v['contest'][:8]}... | SU: {v['su']:.4f} | ATS: {v['ats']:.4f} | Diff: {v['diff']:.4f}")
