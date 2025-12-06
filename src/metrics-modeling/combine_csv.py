import pandas as pd

# Load historical (weeks 1–10)
df_train = pd.read_csv("./data/competition_metrics.csv")

# Load week 11 (predictions only)
df_week11 = pd.read_csv("./data/competition_metrics_week_15.csv")

# Add required columns to match training
df_week11["HomeScore"] = None
df_week11["AwayScore"] = None
df_week11["Winner"] = None

# Align column order
df_week11 = df_week11[df_train.columns]

print(f"df_train rows: {len(df_train)}")
print(f"df_week11 rows: {len(df_week11)}")


# Combine
df_full = pd.concat([df_train, df_week11], ignore_index=True)

# Write output
df_full.to_csv("./data/competition_metrics_full.csv", index=False)

print("✅ Combined dataset written to /data/competition_metrics_full.csv")














