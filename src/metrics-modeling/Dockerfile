# MetricBot — deetsMeter prediction pipeline (internal service).
# Design: docs/metrics-modeling/metrics-microservice-deetsmeter.md
#
# Build from the REPO ROOT so the build context matches the other
# services' pipelines:
#   docker build -f src/metrics-modeling/Dockerfile -t metricbot:local .

# Pinned to a patch release (not the floating 3.12-slim tag) so a rebuild
# of the same commit produces the same runtime. Bump deliberately.
FROM python:3.12.11-slim-bookworm

# psql is the extraction tool (deliberate: no Python DB driver — see
# metricbot/extract.py). curl is for container-level health checks.
# postgresql-client-15 pins the Debian bookworm major so security patches
# still flow while a rebuild cannot silently jump major versions.
RUN apt-get update \
    && apt-get install -y --no-install-recommends postgresql-client-15 curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Dependencies first so layer caching survives source edits.
COPY src/metrics-modeling/requirements.txt ./requirements.txt
COPY src/metrics-modeling/requirements-service.txt ./requirements-service.txt
RUN pip install --no-cache-dir -r requirements.txt -r requirements-service.txt

COPY src/metrics-modeling/metricbot ./metricbot
COPY src/metrics-modeling/sql ./sql

# Non-root, matching the platform's container posture.
RUN useradd --create-home --uid 10001 metricbot \
    && chown -R metricbot:metricbot /app
USER metricbot

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

# Single worker: runs are short, and the LinearRegression fit is
# CPU-bound — concurrency comes from replicas, not threads.
CMD ["uvicorn", "metricbot.service:app", "--host", "0.0.0.0", "--port", "8080", "--workers", "1"]
