import pandas as pd

# Oklahoma vs LSU - Direct comparison from your DTOs

print("="*80)
print("OKLAHOMA vs LSU - COMPREHENSIVE METRIC COMPARISON")
print("="*80)

metrics = {
    'OFFENSE': {
        'Yards Per Play': (5.39, 5.56, 'higher_better'),
        'Success Rate': (0.4371, 0.4378, 'higher_better'),
        'Explosive Rate': (0.0647, 0.0625, 'higher_better'),
        'Points Per Drive': (5.20, 5.47, 'higher_better'),
        '3rd/4th Down Rate': (0.4350, 0.4109, 'higher_better'),
        'RedZone TD Rate': (0.7417, 0.4409, 'higher_better'),
        'RedZone Score Rate': (0.9400, 0.7515, 'higher_better'),
        'Time Possession': (0.52, 0.52, 'higher_better'),
    },
    'DEFENSE': {
        'Opp Yards Per Play': (4.38, 5.14, 'lower_better'),
        'Opp Success Rate': (0.3650, 0.4440, 'lower_better'),
        'Opp Explosive Rate': (0.0562, 0.0563, 'lower_better'),
        'Opp Points Per Drive': (3.16, 4.13, 'lower_better'),
        'Opp 3rd/4th Down': (0.3197, 0.3985, 'lower_better'),
        'Opp RedZone TD': (0.3778, 0.5722, 'lower_better'),
    },
    'SPECIAL_TEAMS': {
        'FG % (Shrunk)': (0.7727, 0.7955, 'higher_better'),
        'Penalty Yards/Play': (0.47, 0.83, 'lower_better'),
        'Field Pos Diff': (-6.21, -8.22, 'higher_better'),
        'Turnover Margin/Drive': (-0.019, 0.052, 'higher_better'),
    },
    'SCORING': {
        'Pts Scored Avg': (27.27, 22.64, 'higher_better'),
        'Pts Allowed Avg': (14.00, 18.45, 'lower_better'),
        'Margin Win Avg': (19.00, 13.29, 'higher_better'),
        'Margin Loss Avg': (12.50, 11.75, 'lower_better'),
    }
}

okla_wins = 0
lsu_wins = 0
ties = 0

for category, cat_metrics in metrics.items():
    print(f"\n{category}:")
    print(f"{'Metric':<25} {'Oklahoma':>12} {'LSU':>12} {'Winner':>12} {'Advantage':>12}")
    print("-" * 80)
    
    for metric_name, (okla_val, lsu_val, direction) in cat_metrics.items():
        if direction == 'higher_better':
            if okla_val > lsu_val:
                winner = "Oklahoma"
                okla_wins += 1
                advantage = f"+{((okla_val - lsu_val) / lsu_val * 100):.1f}%"
            elif lsu_val > okla_val:
                winner = "LSU"
                lsu_wins += 1
                advantage = f"+{((lsu_val - okla_val) / okla_val * 100):.1f}%"
            else:
                winner = "TIE"
                ties += 1
                advantage = "0.0%"
        else:  # lower_better
            if okla_val < lsu_val:
                winner = "Oklahoma"
                okla_wins += 1
                advantage = f"+{((lsu_val - okla_val) / okla_val * 100):.1f}%"
            elif lsu_val < okla_val:
                winner = "LSU"
                lsu_wins += 1
                advantage = f"+{((okla_val - lsu_val) / lsu_val * 100):.1f}%"
            else:
                winner = "TIE"
                ties += 1
                advantage = "0.0%"
        
        okla_str = f"{okla_val:.4f}" if isinstance(okla_val, float) else str(okla_val)
        lsu_str = f"{lsu_val:.4f}" if isinstance(lsu_val, float) else str(lsu_val)
        
        print(f"{metric_name:<25} {okla_str:>12} {lsu_str:>12} {winner:>12} {advantage:>12}")

print(f"\n{'='*80}")
print(f"SCOREBOARD:")
print(f"{'='*80}")
print(f"Oklahoma wins {okla_wins} categories")
print(f"LSU wins {lsu_wins} categories")
print(f"Ties: {ties}")
print(f"\nOklahoma advantage: {okla_wins - lsu_wins:+d} categories")

print(f"\n{'='*80}")
print(f"KEY INSIGHTS:")
print(f"{'='*80}")

# Calculate massive differences
print("\nOklahoma's BIGGEST advantages:")
print("  RedZone TD Rate: 74.2% vs 44.1% → 68% better!")
print("  RedZone Score Rate: 94.0% vs 75.2% → 25% better!")
print("  Opp Points/Drive: 3.16 vs 4.13 → 31% better defense!")
print("  Pts Scored Avg: 27.3 vs 22.6 → 21% better!")
print("  Pts Allowed Avg: 14.0 vs 18.5 → 32% better defense!")

print("\nLSU's advantages:")
print("  Points Per Drive: 5.47 vs 5.20 → 5% better")
print("  Yards Per Play: 5.56 vs 5.39 → 3% better")
print("  Turnover Margin: +0.052 vs -0.019 → Small edge")

print(f"\n{'='*80}")
print(f"CONCLUSION:")
print(f"{'='*80}")
print("Oklahoma is CLEARLY superior in:")
print("  ✓ Scoring efficiency (27.3 vs 22.6 PPG)")
print("  ✓ Defense (14.0 vs 18.5 PPG allowed)")
print("  ✓ RedZone execution (94% vs 75% scoring rate)")
print("  ✓ Defensive RedZone (38% vs 57% TD allowed)")
print("\nLSU has marginal advantages in:")
print("  • Yards per play (3% better)")
print("  • Points per drive (5% better)")
print("\n🚨 THE MODEL IS WRONG: Oklahoma should be heavily favored")
print("   Vegas agrees: Oklahoma -10.5 point favorite")
print("   Model says: LSU 53% favorite")
