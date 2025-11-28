import pandas as pd

df = pd.read_csv('./data/competition_metrics_full.csv')
df = df[df['Winner'].notna() & df['Spread'].notna()].copy()
df['Margin'] = df['HomeScore'] - df['AwayScore']

# Test both formulas
df['CoveredOld'] = df['Margin'] > df['Spread']
df['CoveredNew'] = (df['Margin'] + df['Spread']) > 0

# Check if they match
print(f'Formula comparison:')
print(f'  Margin > Spread == (Margin + Spread) > 0: {df["CoveredOld"].equals(df["CoveredNew"])}')
print()
print('Sample rows:')
print(df[['HomeScore', 'AwayScore', 'Margin', 'Spread', 'CoveredOld', 'CoveredNew']].head(10).to_string(index=False))
