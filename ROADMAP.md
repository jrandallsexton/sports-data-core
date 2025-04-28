# SportDeets Architecture & Roadmap

## Project Vision
Build a scalable, event-driven NCAA football analysis platform that leverages LLMs for insights, supports weekly pick'em games, and provides a blazing fast UI experience.

---

## Current Architecture Overview

| Layer | Technology | Notes |
|:------|:-----------|:------|
| Frontend (UI) | React App on **Azure Static Web Apps** | ✅ Super cheap, fast global delivery, easy GitHub integration |
| API Layer | **Azure Container Apps (ACA)** | API Gateway, ContestService, FranchiseService, NotificationService, PlayerService, ProducerService, ProviderService, SeasonService, VenueService |
| LLM Analysis | **Ollama** models in **ACA** | Specialized containers per conference/team |
| Event Bus | Azure Service Bus or Dapr Pub/Sub (via ACA) | Structured knowledge events (e.g., analysis completion) |
| Database | Local MongoDB (external data), Local MSSQL (canonical data) initially | Azure SQL or CosmosDB later for cloud production |
| Authentication | Azure Static Web App Auth (short-term) | Future: Custom Identity Provider integration |

---

## Services Overview

- **api:** API Gateway for routing frontend requests to backend services.
- **contest:** Specializes in handling matchups, contests, and user picks (details coming).
- **franchise:** Manages team and conference metadata (details coming).
- **notification:** Future service to handle email, SMS, and push notifications.
- **player:** Responsible for athlete data management (details coming).
- **producer:** Processes external JSON data into canonical domain models stored in MSSQL. Broadcasts domain/integration events for other services to react.
- **provider:** Gathers data from external sources (ESPN, CBS, Yahoo!, sportsData.io, etc.). Stores external JSON into MongoDB and broadcasts events once resources are acquired.
- **season:** Manages season-specific metadata (weeks, schedules) (details coming).
- **venue:** Handles stadium and location information (details coming).

---

## System Architecture Overview

[External APIs (ESPN, CBS, etc.)]
    ⬇
[ProviderService]
    - Fetches data
    - Stores raw JSON in MongoDB
    - Broadcasts 'ExternalDataAvailable' event
    ⬇
[ProducerService]
    - Consumes ExternalDataAvailable event
    - Transforms into canonical domain objects
    - Persists to MSSQL
    - (Future) Broadcasts refined domain events
    ⬇
[Domain Services (ContestService, FranchiseService, PlayerService, VenueService)]
    - Specialize in consuming domain-specific events
    - Serve API data to the Frontend (UI)

---

## Roadmap

### Phase 1: Dev Infrastructure (Now)
- [x] Create Azure Static Web App for dev.sportdeets.com
- [ ] Connect Static Web App to GitHub repo (auto-deploy on push to `dev` branch)
- [ ] Deploy simple "hello world" React App to Static Web App
- [ ] Set up Azure Container Apps environment (ACA Env)
- [ ] Build basic ContestService container and deploy to ACA
- [ ] Expose ContestService API to Static Web App

### Phase 2: Core Event Flow (Short-Term)
- [ ] Connect ProviderService to Azure Service Bus (working locally with MongoDB)
- [ ] Connect ProducerService to Azure Service Bus (working locally with MSSQL)
- [ ] Publish ExternalDataAvailable and domain model events
- [ ] Consume domain events in ContestService, FranchiseService, VenueService, PlayerService
- [ ] UI consumes ContestService API to display matchups

### Phase 3: Specialization and Scaling (Medium-Term)
- [ ] Expand AnalyzerService into multiple specialized analyzers (e.g., SECAnalyzer, BigTenAnalyzer)
- [ ] Implement error handling and validation of LLM output before event publishing
- [ ] Add scaling rules for each ACA service based on QPS

### Phase 4: Feature Expansion (Mid-Term)
- [ ] User Authentication (Google, GitHub, Microsoft ID)
- [ ] User Pick'em Entries
- [ ] Weekly Pick Summaries (auto-generated via LLM)
- [ ] Leaderboards
- [ ] Admin Dashboard (manage weeks, lock pick deadlines)

### Phase 5: Advanced Analytics (Long-Term)
- [ ] Team-specific Analyzer services (AlabamaAnalyzer, OhioStateAnalyzer)
- [ ] Dynamic content generation (e.g., "Keys to Victory" blurbs per matchup)
- [ ] Predictive analysis models (Win Probability)
- [ ] Notifications (email, app push) based on picks/contest updates

### Phase 6: Infrastructure Hardening (Long-Term)
- [ ] Implement GitOps with FluxCD for ACA (optional)
- [ ] Set up Azure Monitor for end-to-end observability
- [ ] Azure Front Door integration for global load balancing
- [ ] Cost optimization review (e.g., ACA scale-to-zero, Static Web App optimization)

---

## Guiding Principles
- **Event-Driven Design:** Services communicate asynchronously through events.
- **Service Specialization:** Each service owns its own domain boundary.
- **Stateless APIs:** Services remain stateless for scalability and fault-tolerance.
- **Auditability:** Log raw external data and model outputs before processing.
- **Resilience:** Failure in one part (e.g., external API outage) does not cascade.
- **Rapid Iteration:** Optimize for speed today, scale and harden tomorrow.

---

## Stretch Goals
- Mobile app (React Native)
- Public API for third-party developers
- Seasonal "March Madness" expansion (college basketball pick'em)
- Real-time event ingestion (e.g., injury updates, weather alerts)

---

> "Provider drives Producer, Producer drives the Domain." — SportDeets Design Principle

---

*Document version: v1.2*


---

> "Ship small, ship fast, stay flexible."

---

*Document version: v1.0*
