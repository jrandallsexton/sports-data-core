import pandas as pd
import json

# === Config ===
PICKEM_GROUP_ID = "aa7a482f-2204-429a-bb7c-75bc2dfef92b"  # Swap as needed
WEEK_NUMBER = 11

# === Load predictions CSV ===
df = pd.read_csv("./data/week11_predictions.csv")

# === Determine picked team ===
df["PickedFranchiseSeasonId"] = df.apply(
    lambda row: row["HomeFranchiseSeasonId"] if row["PredictedWinner"] == "HOME"
    else row["AwayFranchiseSeasonId"],
    axis=1
)

# === Build DTOs ===
dto_list = [
    {
        "pickemGroupId": PICKEM_GROUP_ID,
        "contestId": row["ContestId"],
        "pickType": "StraightUp",
        "franchiseSeasonId": row["PickedFranchiseSeasonId"],
        "week": WEEK_NUMBER
    }
    for _, row in df.iterrows()
]

# === Write to file ===
with open("./data/metricbot_week11_dtos.json", "w") as f:
    json.dump(dto_list, f, indent=2)

print(f"✅ {len(dto_list)} DTOs written to ./data/metricbot_week11_dtos.json")
