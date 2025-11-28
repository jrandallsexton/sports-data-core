import pandas as pd
import json

# Load raw predictions
df_su = pd.read_csv('./data/predictions_straightup_raw.csv')
df_ats = pd.read_csv('./data/predictions_ats_raw.csv')

# Load DTOs
with open('./data/contest_predictions.json') as f:
    dtos = json.load(f)

# Check first 3 contests
print("Verifying DTO generation logic:\n")
print("="*80)

for i in range(3):
    contest_id = df_su.iloc[i]['ContestId']
    
    # Get raw data
    su_row = df_su.iloc[i]
    ats_row = df_ats.iloc[i]
    
    # Get DTOs for this contest
    su_dto = [d for d in dtos if d['ContestId'] == contest_id and d['PredictionType'] == 1][0]
    ats_dto = [d for d in dtos if d['ContestId'] == contest_id and d['PredictionType'] == 2][0]
    
    print(f"\nContest {i+1}: {contest_id[:20]}...")
    print(f"\nSU Prediction:")
    print(f"  Home ID: {su_row['HomeFranchiseSeasonId'][:20]}...")
    print(f"  Away ID: {su_row['AwayFranchiseSeasonId'][:20]}...")
    print(f"  Home Win Prob: {su_row['WinProbability']:.4f}")
    print(f"  Away Win Prob: {1 - su_row['WinProbability']:.4f}")
    print(f"  PredictedLabel: {su_row['PredictedLabel']} ({'Home' if su_row['PredictedLabel'] == 1 else 'Away'})")
    print(f"  DTO Winner: {su_dto['WinnerFranchiseSeasonId'][:20]}...")
    print(f"  DTO Probability: {su_dto['WinProbability']:.4f}")
    
    if su_row['PredictedLabel'] == 1:
        expected_id = su_row['HomeFranchiseSeasonId']
        expected_prob = su_row['WinProbability']
    else:
        expected_id = su_row['AwayFranchiseSeasonId']
        expected_prob = 1 - su_row['WinProbability']
    
    su_correct = (su_dto['WinnerFranchiseSeasonId'] == expected_id and 
                  abs(su_dto['WinProbability'] - expected_prob) < 0.001)
    print(f"  ✓ CORRECT" if su_correct else f"  ✗ WRONG!")
    
    print(f"\nATS Prediction:")
    print(f"  Home ID: {ats_row['HomeFranchiseSeasonId'][:20]}...")
    print(f"  Away ID: {ats_row['AwayFranchiseSeasonId'][:20]}...")
    print(f"  Home Cover Prob: {ats_row['HomeCoverProbability']:.4f}")
    print(f"  Away Cover Prob: {ats_row['AwayCoverProbability']:.4f}")
    print(f"  PredictedLabel: {ats_row['PredictedLabel']} ({'Home' if ats_row['PredictedLabel'] == 1 else 'Away'})")
    print(f"  DTO Winner: {ats_dto['WinnerFranchiseSeasonId'][:20]}...")
    print(f"  DTO Probability: {ats_dto['WinProbability']:.4f}")
    
    if ats_row['PredictedLabel'] == 1:
        expected_id = ats_row['HomeFranchiseSeasonId']
        expected_prob = ats_row['HomeCoverProbability']
    else:
        expected_id = ats_row['AwayFranchiseSeasonId']
        expected_prob = ats_row['AwayCoverProbability']
    
    ats_correct = (ats_dto['WinnerFranchiseSeasonId'] == expected_id and 
                   abs(ats_dto['WinProbability'] - expected_prob) < 0.001)
    print(f"  ✓ CORRECT" if ats_correct else f"  ✗ WRONG!")
    
    print("="*80)
