import pandas as pd

df = pd.read_csv('./data/competition_metrics.csv')

# Filter to games with spreads
df = df[df['Spread'].notna()].copy()

# Calculate margin
df['Margin'] = df['HomeScore'] - df['AwayScore']

# Test different cover formulas
df['CoveredFormula1'] = df['Margin'] > df['Spread']  # Our current formula

print("Testing Spread Convention:\n")
print("Sample games:")
print("="*80)

for idx in [0, 3, 5, 6]:
    row = df.iloc[idx]
    margin = row['Margin']
    spread = row['Spread']
    home_score = row['HomeScore']
    away_score = row['AwayScore']
    covered = row['CoveredFormula1']
    
    print(f"\nGame {idx}:")
    print(f"  Score: Home {home_score:.0f} - Away {away_score:.0f} (Margin: {margin:+.1f})")
    print(f"  Spread: {spread:+.1f}")
    print(f"  Formula (Margin > Spread): {margin:.1f} > {spread:.1f} = {covered}")
    
    if spread > 0:
        print(f"  → Spread is POSITIVE: Home is underdog (gets {spread:.1f} points)")
        print(f"  → Adjusted score: Home {home_score + spread:.1f} - Away {away_score:.1f}")
        if covered:
            print(f"  → HOME COVERED (won or lost by less than {spread:.1f})")
        else:
            print(f"  → AWAY COVERED (home lost by more than {spread:.1f})")
    else:
        print(f"  → Spread is NEGATIVE: Home is favorite (must win by {abs(spread):.1f})")
        if covered:
            print(f"  → HOME COVERED (won by more than {abs(spread):.1f})")
        else:
            print(f"  → AWAY COVERED (home won by less than {abs(spread):.1f} or lost)")
