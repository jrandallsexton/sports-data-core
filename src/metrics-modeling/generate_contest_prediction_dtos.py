import pandas as pd
import json

# Load CSVs
df_su = pd.read_csv("./data/predictions_straightup_raw.csv")
df_ats = pd.read_csv("./data/predictions_ats_raw.csv")

# Define enum mappings
PICKTYPE_STRAIGHT_UP = 1
PICKTYPE_ATS = 2

# Normalize SU predictions
su_records = []
for _, row in df_su.iterrows():
    # Always use Home Team for consistency with UI display
    winner_id = row["HomeFranchiseSeasonId"]
    win_prob = round(float(row["WinProbability"]), 4)

    su_records.append({
        "ContestId": row["ContestId"],
        "WinnerFranchiseSeasonId": winner_id,
        "WinProbability": win_prob,
        "PredictionType": PICKTYPE_STRAIGHT_UP,
        "ModelVersion": row["ModelVersion"]
    })


# Normalize ATS predictions
ats_records = []

for _, row in df_ats.iterrows():
    home_cover_prob = float(row["HomeCoverProbability"])
    
    contest_id = row["ContestId"]
    home_id = row["HomeFranchiseSeasonId"]
    
    # Always use Home Team for consistency with UI display
    winner_id = home_id
    win_prob = round(home_cover_prob, 4)

    ats_records.append({
        "ContestId": contest_id,
        "WinnerFranchiseSeasonId": winner_id,
        "WinProbability": win_prob,
        "PredictionType": PICKTYPE_ATS,
        "ModelVersion": row["ModelVersion"]
    })

# Combine & export
all_predictions = su_records + ats_records

with open("./data/contest_predictions.json", "w") as f:
    json.dump(all_predictions, f, indent=2)

print("✅ DTOs written to ./data/contest_predictions.json")

















