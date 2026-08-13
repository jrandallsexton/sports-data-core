"""MetricBot — the deetsMeter prediction pipeline as a single CLI.

Season-launch MVP Phase A (docs/metrics-modeling/metrics-microservice-deetsmeter.md):
replaces Generate-Predictions.ps1 + inter-stage CSVs + the manual Postman
POST with one entry point:

    python -m metricbot run-week [--sport ncaaf|nfl] [--dry-run] [--dump-intermediate]

Math is ported verbatim from the prototype scripts (predict_straightup.py,
predict_ats.py, generate_contest_prediction_dtos.py) — the goal is
output parity, not model improvement.
"""

__version__ = "1.0.0"

# Stamped on every prediction row; bump when the MATH changes, not the plumbing.
MODEL_VERSION = "MetricBot-v1.1.1"
