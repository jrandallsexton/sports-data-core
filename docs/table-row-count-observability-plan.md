# Table Row Count Observability Plan

## Objective
Implement a mechanism to periodically capture and visualize PostgreSQL table row counts to monitor data ingestion velocity and table growth, particularly during historical sourcing runs.

## Approach
We will use **Option 1: The Observability Route (Prometheus + Grafana)**. 
This approach leverages the existing observability stack to expose table row counts as metrics, allowing Grafana to handle the visualization of deltas and trends without requiring custom persistence or complex SQL queries.

## Key Decisions
1.  **Data Destination:** Prometheus (via Pushgateway).
2.  **Visualization:** Grafana dashboards.
3.  **Execution Context:** Kubernetes CronJob (decoupled from the `SportsData.Producer` application pods).
4.  **Count Method:** Exact `COUNT(*)` (since the frequency is low enough to mitigate performance concerns).
5.  **Execution Frequency:** Every 10-15 minutes (e.g., `*/15 * * * *`).
6.  **Execution Condition:** Always running (provides baseline growth metrics with negligible overhead at this frequency).

## Architecture Rationale
*   **Why not an `IHostedService` in Producer?** If the `Producer` service scales to multiple pods, each pod would run the query simultaneously unless distributed locking is introduced. This wastes database resources and adds unnecessary complexity.
*   **Why not a Hangfire Job?** While Hangfire handles distributed locking (ensuring only one execution), placing infrastructure observability concerns inside the application's background job processor mixes application logic with infrastructure monitoring. It "feels wrong" because it violates the separation of concerns.
*   **Why a Kubernetes CronJob + Pushgateway?** A CronJob is a native Kubernetes resource designed for periodic tasks. It runs independently of the application pods, consuming no application resources. Because CronJob pods are ephemeral (they terminate after execution), Prometheus cannot reliably scrape them. Therefore, the CronJob will push its metrics to a Prometheus Pushgateway, which Prometheus then scrapes at its regular interval.

## Implementation Steps (Draft)

### 1. Metric Definition
*   Define a new metric (e.g., a Gauge) to represent the row count.
*   Include labels/tags for the `table_name` to allow filtering and grouping in Grafana.

### 2. Data Collection Mechanism (Kubernetes CronJob)
*   Create a lightweight container image (e.g., `postgres:15-alpine` which includes the `psql` client).
*   Write a shell script to execute the SQL query against the PostgreSQL database.
*   Format the output into Prometheus exposition format.
*   Push the metrics to a Prometheus Pushgateway (since CronJobs are ephemeral and cannot be scraped reliably).
*   Define a Kubernetes `CronJob` manifest to schedule the execution.

### 3. Observability Configuration
*   Deploy a Prometheus Pushgateway to the cluster (if not already present).
*   Configure Prometheus to scrape the Pushgateway.

### 4. Secret Management (Future/Ongoing)
*   Currently, the database credentials for the CronJob will be created imperatively (e.g., `kubectl create secret generic postgres-credentials ...`).
*   *Future consideration:* Implement a declarative secret management solution (like Sealed Secrets or External Secrets Operator) to allow storing encrypted secrets in the GitOps repository.

### 5. Grafana Dashboard Creation
*   Create a new Grafana dashboard or add panels to an existing one.
*   Use PromQL queries to visualize:
    *   Current row counts per table.
    *   Total row count across all relevant tables.
    *   Deltas/velocity (e.g., rows added per minute) using functions like `increase()` or `rate()`.

## Open Questions
*   **Accuracy vs. Performance:** Do we need exact row counts (slower, resource-intensive on large tables) or are fast statistical estimates sufficient for tracking velocity?
*   **Frequency:** How often should we poll the database for these counts?
*   **Scope:** Should this run continuously, or only when a historical sourcing run is active? If only during active runs, how do we determine if a run is active?
