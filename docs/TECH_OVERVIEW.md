# SportDeets Technical Overview

## Purpose
SportDeets is a cloud-native application designed to support private group-based NCAA football pick'em games.  
It enhances user engagement through matchup analysis, consensus picks, and automated event-driven updates driven by LLMs.

---

## System Components

### 1. Frontend (UI)
- **Framework:** React
- **Hosting:** Azure Static Web Apps
- **Domain:** dev.sportdeets.com (development environment)
- **Responsibilities:**
  - Display matchups, consensus picks, standings.
  - Handle user authentication (short-term via Static Web Apps built-in auth).
  - Interact with backend services through secured APIs.
  - Completely unaware of LLM processes — only consumes structured data.

### 2. API Services
- **Hosting:** Azure Container Apps (ACA)
- **Language:** .NET (C#)
- **Key Services:**
  - **ContestService:** Manage matchups, contests, picks, consensus data.
  - **PickService (future):** Manage user pick submissions and validation.
  - **AnalyzerService:** Interact with LLMs, publish event-driven insights.

### 3. LLM Analysis Services
- **Hosting:** Azure Container Apps (ACA)
- **Model Runtime:** Ollama (initially), using lightweight LLMs.
- **Responsibilities:**
  - Analyze matchups, generate summary insights.
  - Specialize by conference (SEC, BigTen, etc.), later by team.
  - Publish structured events after analysis (e.g., `ConferenceAnalysisCompleted`).

### 4. Event Bus
- **Choice:** Azure Service Bus (initial), with future Dapr Pub/Sub integration possible.
- **Responsibilities:**
  - Decouple model output from service ingestion.
  - Deliver structured domain events reliably across services.
  - Support retries, dead-letter queues, and event ordering as needed.

### 5. Database
- **Options:** Azure SQL, CosmosDB, or Postgres (final choice pending workload characteristics).
- **Responsibilities:**
  - Persist matchup data, contest entries, pick records, consensus statistics.
  - Separate storage layer from event-driven model outputs (auditability).

---

## Communication Flow

```plaintext
[Static Web App (React)]
    ⬇️ API Calls
[ContestService API]
    ⬇️ Database Read/Write
[Background]
    ⬇️
[AnalyzerService (periodic analysis or event trigger)]
    ⬇️
[LLM Invocation (Ollama)]
    ⬇️
[Event Published to Event Bus]
    ⬇️
[ContestService Listens → Updates Matchup/Contest Data]
