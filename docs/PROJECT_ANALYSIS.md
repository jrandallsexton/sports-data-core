# sportDeets Platform Analysis

*Analysis Date: February 1, 2026*

---

## Table of Contents

1. [Project Purpose](#1-project-purpose)
2. [Architecture](#2-architecture)
3. [UI Analysis](#3-ui-analysis)
4. [Product Viability & Monetization](#4-product-viability--monetization)

---

## 1. Project Purpose

sportDeets is a hobby project and professional portfolio piece -- a sports analytics and pick'em platform currently focused on NCAA college football (with NFL infrastructure in place). The system ingests data from ESPN's public APIs, transforms it into a canonical domain model, and exposes it through both a REST API and a React-based consumer application. It is designed primarily as a vehicle for exercising and demonstrating proficiency across a broad range of modern distributed systems technologies.

The platform serves three purposes:

**Skills Platform**: A living codebase for staying sharp across the full stack -- .NET backend, React frontend, event-driven messaging, containerized deployment, observability, and infrastructure-as-code. The breadth of technologies exercised (EF Core, MassTransit, Hangfire, OpenTelemetry, SignalR, Firebase, CQRS/MediatR, self-hosted Kubernetes) makes this an unusually comprehensive single-developer project.

**Portfolio Piece**: Demonstrates end-to-end system design to prospective employers -- from data ingestion and domain modeling through event-driven processing, REST API design, real-time UI, and production operations on bare metal Kubernetes.

**Functional Product**: A pick'em web application ("sportDeets") where users join leagues, make weekly game picks against the spread, track leaderboards, and optionally consume AI-generated game insights. The consumer product is real and usable, though commercial viability is not the primary goal.

---

## 2. Architecture

### 2.1 High-Level Topology

```
ESPN APIs
    |
    v
Provider Service ──> Azure Blob Storage (Data Lake)
    |
    v  (MassTransit: DocumentCreated events)
Producer Service ──> PostgreSQL (canonical domain model)
    |
    v  (HTTP + MassTransit events)
Domain Services (Contest, Franchise, Player, Season, Venue, Notification)
    |
    v
API Gateway (SportsData.Api) ──> Firebase Auth
    |                               |
    v                               v
React SPA (sd-ui)           SignalR Hub (live scores)
```

### 2.2 Solution Structure

The solution contains **11 source projects** and **22 test projects** (unit + integration for each service):

| Project | Role |
|---------|------|
| **SportsData.Core** | Shared library: HTTP clients, DTOs, messaging, configuration, observability, validation |
| **SportsData.Producer** | Data transformation engine. 100+ document processors convert ESPN JSON into canonical entities. Owns the primary PostgreSQL schema (150+ DbSets). Hangfire for job scheduling |
| **SportsData.Provider** | Data ingestion. Fetches raw JSON from ESPN, caches to Azure Blob Storage, emits DocumentCreated events |
| **SportsData.Api** | API gateway and consumer-facing backend. Firebase JWT auth, HATEOAS REST endpoints, SignalR hub for live updates, user/league/pick management |
| **SportsData.Contest** | Game/contest domain service |
| **SportsData.Franchise** | Team/franchise domain service |
| **SportsData.Player** | Athlete domain service |
| **SportsData.Season** | Season/calendar domain service |
| **SportsData.Venue** | Stadium/location domain service |
| **SportsData.Notification** | Push notification domain service |
| **SportsData.ProcessorGen** | Code generation utility for document processors |

### 2.3 Architectural Style

**Modular monolith with extraction readiness.** Each domain has its own project, HTTP client interfaces, and can be extracted into an independent microservice. Currently, the domain services route back to the Producer's database through typed HTTP clients, making the Producer the authoritative data store.

### 2.4 Key Patterns

| Pattern | Implementation |
|---------|----------------|
| **CQRS** | MediatR-based command/query separation across all services |
| **Outbox** | MassTransit EF Core outbox for guaranteed event delivery |
| **Document Processor** | Attribute-registered processors (`[DocumentProcessor(provider, sport, type)]`) handle ESPN JSON-to-entity transformation |
| **Wholesale Replacement** | Idempotent "delete existing + insert new" for repeated documents |
| **HATEOAS** | All paginated API responses include self/first/last/prev/next navigation links |
| **Result Pattern** | `Success<T>` / `Failure<T>` wrappers with `ValidationFailure` arrays |
| **Factory** | Client factories (`IFranchiseClientFactory`, etc.) resolve sport-specific implementations |
| **Pipeline Behaviors** | MediatR behaviors for query caching (Redis) and cross-cutting concerns |

### 2.5 Infrastructure & Operations

| Component | Technology |
|-----------|------------|
| **Runtime** | .NET 10, ASP.NET Core 10 |
| **Database** | PostgreSQL (Npgsql + EF Core 10) |
| **Cache** | Redis (distributed), disk cache (ESPN responses) |
| **Messaging** | MassTransit 8.3 over RabbitMQ (local) / Azure Service Bus (production) |
| **Job Scheduling** | Hangfire 1.8 with PostgreSQL storage |
| **Observability** | OpenTelemetry 1.14 (traces + metrics), Prometheus, Serilog (Seq + OTLP sinks) |
| **Auth** | Firebase Authentication (Google OAuth, email/password) |
| **Blob Storage** | Azure Blob Storage for raw ESPN documents and images |
| **Configuration** | Azure App Configuration + Key Vault |
| **CI/CD** | GitHub Actions (test/deploy) + Azure Pipelines (build/containerize) |
| **Hosting** | Self-hosted Kubernetes on bare metal (4 nodes, 24 cores, 126 GB RAM, 4 TB NVMe) |
| **Cloud** | Azure (Service Bus, Blob, App Configuration, Key Vault, Static Web Apps) |
| **Frontend Hosting** | Azure Static Web Apps |

### 2.6 Architecture Assessment

**Strengths:**
- Clean separation of concerns across domain boundaries. Each service owns its slice of the problem.
- The document processor framework is well-designed -- attribute-based registration, generic base classes, and the wholesale replacement strategy make adding new data sources or sport types mechanical work.
- The outbox pattern and MassTransit integration provide genuine reliability guarantees for the event pipeline.
- The external ID mapping system (`IHasExternalIds`) is a sound approach for multi-source data reconciliation.
- Comprehensive test coverage with 240+ tests and both unit and integration test projects for every service.
- The HATEOAS API design is thoughtful and would support third-party consumers without documentation dependency.

**Areas to watch:**
- The Producer service is the gravitational center of the system. At 150+ DbSets and 100+ document processors, it carries significant complexity. The migration history (40+ migrations in 6 months) reflects rapid schema evolution, which makes migration squashing (the current branch) a practical necessity.
- The domain services (Contest, Franchise, etc.) appear to be thin wrappers that route back to the Producer via HTTP. Until these services own their own data stores, the "microservice readiness" is architectural intent more than operational reality.
- The SportsData.Core shared library is large and contains infrastructure concerns (MassTransit setup, EF Core registration, HTTP client factories). Changes here ripple across all services.

---

## 3. UI Analysis

### 3.1 Technology Stack

| Layer | Technology |
|-------|------------|
| **Framework** | React 19.1 (Create React App) |
| **Routing** | React Router DOM 7.5 |
| **State Management** | React Context API (5 contexts) + custom hooks. No Redux |
| **Charting** | Recharts 2.15, Chart.js 4.4 (secondary) |
| **Maps** | Google Maps API via @react-google-maps/api |
| **UI Components** | MUI 7.3 (selective usage), custom CSS |
| **Icons** | Lucide React, React Icons |
| **Real-Time** | SignalR (ASP.NET Core hub) |
| **Auth** | Firebase Auth SDK |
| **HTTP Client** | Axios with interceptors (auto token refresh, 401 retry) |
| **Styling** | Component-scoped CSS files, CSS custom properties for theming |

### 3.2 Application Structure

**Public Pages** (unauthenticated):
- Landing page with hero section, feature overview, and "How It Works" flow
- Sign-up (Google OAuth, email/password; Facebook and Apple placeholders)
- Gallery, Terms of Service, Privacy Policy
- Maintenance page

**Authenticated Pages** (20+ routes behind `PrivateRoute`):

| Route | Page | Description |
|-------|------|-------------|
| `/app` | Home | Dashboard with pick accuracy charts, AI accuracy, leaderboard widget, rankings, news, league membership |
| `/app/picks/:leagueId?` | Picks | Weekly matchup picks -- card or grid view, ATS format, confidence points |
| `/app/warroom` | War Room | Advanced franchise metrics grid for power users |
| `/app/leaderboard` | Leaderboard | League standings, weekly scores, week overview (3 tabs) |
| `/app/map` | Game Map | Google Maps visualization of game locations |
| `/app/messageboard` | Message Board | Community discussion threads |
| `/app/settings` | Settings | Theme toggle, notification preferences |
| `/app/league` | Leagues | List/create/discover/join leagues |
| `/app/league/create` | Create League | Private league creation form |
| `/app/league/discover` | Discover | Browse and join public leagues |
| `/app/sport/football/ncaa/team/:slug` | Team Card | Team profile: stats, schedule, news, comparison |
| `/app/sport/football/ncaa/contest/:id` | Contest Overview | Game detail: summary, analysis, team stats, win probability, play log, drive metrics, video |
| `/app/admin` | Admin | System health, data audits, competition validation (admin-only) |

### 3.3 Core User Functionality

**Picks & Wagering:**
- Weekly picks against the spread (ATS)
- Confidence points allocation (ranked picks)
- Optimistic UI -- pick selection updates instantly, server confirmation follows
- Auto-lock enforcement before game start time
- Card view (detailed per-game cards) and grid view (compact table)
- Live score merging via SignalR during games
- Pick result visualization (correct/incorrect border coloring)

**Leagues:**
- Create private leagues with invite links
- Discover and join public leagues
- Per-league leaderboards with weekly breakdown
- Bot/synthetic player support for league padding

**Analytics:**
- Home dashboard with pick accuracy over time (bar charts)
- AI prediction accuracy tracking alongside user performance
- War Room with franchise-level metrics
- Team comparison tool (head-to-head stat overlay)
- Contest overview with 8 analysis tabs

**AI Insights:**
- LLM-generated game previews (DeepSeek models)
- Approval/rejection workflow for generated content
- Gated behind subscription (insight unlock model)

**Real-Time:**
- SignalR pushes live game scores, status, possession, clock, and period updates
- ContestUpdatesContext merges live data into matchup cards
- Scoring play flash indicators (2-second highlight)

### 3.4 UI Assessment

**Strengths:**
- The matchup card component (`MatchupCard.jsx`) is well-composed with clean hook extraction (`usePickLocking`, `useTeamSchedule`, `useTeamComparison`). This is the core interaction surface and it's structured for maintainability.
- The Context-based state management is appropriate for this application's complexity. Five contexts with clear responsibilities avoid the overhead of Redux without sacrificing organization.
- The Axios interceptor pattern for auth token refresh and 401 retry is solid and handles tab suspension/resume gracefully.
- SignalR integration for live updates is a genuine differentiator over static pick'em apps.

**Weaknesses:**
- The README roadmap (last updated April 2025) lists several items as TODO that appear to have been implemented since. Some features like "Save user picks to backend API (currently simulated with console.log)" seem stale relative to the actual codebase state.
- Styling is component-scoped CSS without a consistent design system. MUI is used selectively but not comprehensively. This creates visual inconsistency risk as the component count grows.
- The `about` project is a separate React app for a portfolio/documentation site. This adds deployment and maintenance surface area for what could be a static page.
- Facebook and Apple sign-in are stubbed but not implemented -- the signup page shows these buttons but they alert "not implemented."

---

## 4. Product Viability & Monetization

> **Note**: sportDeets is a hobby project and portfolio piece, not a venture-backed startup. The author is aware of the ESPN API dependency risk and intentionally designed the document processor framework to be provider-agnostic (the `[DocumentProcessor(provider, sport, type)]` attribute pattern supports any external source), though each new provider would require substantial implementation work. The following analysis is included for completeness and as an honest assessment of what commercialization would require.

### 4.1 Market Context

The sports pick'em and prediction market is well-established with entrenched competition:

- **Free Tier**: ESPN Pick'em, CBS Sports, Yahoo Pick'em -- these are loss leaders backed by massive media companies that use pick'em as engagement tools to drive ad revenue and ecosystem stickiness.
- **Social/Premium**: Sleeper, Underdog Fantasy -- VC-funded platforms with significant user bases and polished mobile experiences.
- **Gambling-Adjacent**: FanDuel, DraftKings -- regulated operators with real-money wagering, licensed in most US states.

sportDeets competes most directly with the free-tier products from ESPN, CBS, and Yahoo. These platforms have zero-cost user acquisition (they own the content), established brand trust, and mobile apps.

### 4.2 Honest Assessment of Competitive Position

**What sportDeets does well:**
- The data pipeline is genuinely impressive engineering. 130+ entities, 100+ document processors, event-driven architecture with outbox guarantees -- this is production-grade infrastructure.
- AI-generated game insights are a differentiator if the quality is high. Most pick'em platforms don't offer analytical previews.
- The War Room and Contest Overview (8 analysis tabs) cater to a power-user segment that free pick'em apps ignore.
- Self-hosted Kubernetes on bare metal keeps operational costs low relative to cloud-native competitors.

**What works against it:**
- The product is web-only. The competitive set is mobile-first. Users check picks and scores on their phones. The README mentions React Native as a "stretch goal," but without a mobile app this is a significant acquisition and retention barrier.
- NCAA football is seasonal (September-January). This means 7 months of the year the primary product has zero engagement. NFL is infrastructure-ready but not yet shipped to users. Even with NFL, the combined football season still leaves 5+ dead months.
- The free-tier competition (ESPN, CBS, Yahoo) offers the same core functionality -- ATS picks, leaderboards, league management -- at zero cost with better brand recognition, mobile apps, and integrated content ecosystems.
- User authentication currently supports only Google and email. Social login breadth (Apple, Facebook) matters for conversion rates.

### 4.3 Monetization Paths

There are a limited number of viable monetization models for a platform of this nature:

#### Path A: Freemium with AI Insights (Current Direction)

The codebase already has infrastructure for gating AI insights behind a subscription. This is the most natural near-term path.

**Pricing reality check**: Users have low willingness to pay for pick'em features when ESPN/CBS/Yahoo offer the basics for free. The AI insights would need to demonstrably improve pick accuracy (and users would need to believe that) to justify even $5-10/month. The LLM-generated previews would need to surface non-obvious analysis, not just reformat publicly available information.

**Revenue ceiling**: Even optimistically, a niche college football pick'em platform might attract thousands of active users during the season, not tens of thousands. At $5/month for 5 months with a 5% conversion rate on 5,000 users, that's roughly $6,250/season. This doesn't cover infrastructure costs for a project of this complexity.

#### Path B: B2B Data API

The HATEOAS REST API with canonical sports data, multi-source reconciliation, and comprehensive entity coverage could be packaged as a commercial data API.

**Reality check**: Sports data APIs are a real market (Sportradar, Stats Perform, ESPN developer APIs), but they require licensing agreements with leagues and data providers. ESPN's public API (which sportDeets scrapes) is not licensed for commercial redistribution. Building a commercial product on top of another company's undocumented public API is legally fragile and operationally risky -- endpoint changes, rate limiting, or a cease-and-desist could shut down the data pipeline overnight.

#### Path C: Platform/White-Label for Sportsbooks or Media

The pick'em engine, leaderboard system, and analytics dashboard could theoretically be white-labeled for sportsbooks, media outlets, or corporate engagement platforms.

**Reality check**: This requires a sales operation, compliance infrastructure (especially if adjacent to gambling), and enterprise reliability guarantees that a solo/small-team project would struggle to deliver. The engineering is strong, but productizing it for B2B requires a different set of capabilities.

#### Path D: Advertising

Run ads against the free product during football season.

**Reality check**: Ad-supported models require scale (hundreds of thousands of monthly active users) to generate meaningful revenue. Achieving that scale against free competition from ESPN/CBS/Yahoo is the core problem, and advertising doesn't solve user acquisition -- it monetizes it after the fact.

### 4.4 The Core Tension

The engineering investment here is substantial and high-quality. The architecture would be appropriate for a platform serving hundreds of thousands of users. The current product, however, is a seasonal college football pick'em app competing against free alternatives from companies with billion-dollar content ecosystems.

The gap is not technical -- it's distribution and differentiation. The technical foundation could support several pivots, but as a standalone consumer product, the monetization math is difficult:

- **Seasonal usage** compresses the revenue window
- **Free alternatives** cap willingness to pay
- **Mobile absence** limits addressable audience
- **ESPN API dependency** creates platform risk
- **Niche sport focus** (NCAA football only, currently) limits TAM

### 4.5 What Would Improve Viability

If the goal is to make this a revenue-generating product:

1. **Multi-sport support is table stakes.** NFL is infrastructure-ready and should be the immediate priority. NBA and MLB would provide year-round engagement. Without year-round engagement, there is no subscription business.

2. **A mobile app is non-negotiable** for a consumer sports product. React Native is the obvious path given the existing React frontend.

3. **The AI angle is the best differentiator** but needs to be genuinely useful, not just "GPT wrote a paragraph about this game." If AI insights demonstrably help users pick better (with tracked accuracy metrics -- which the platform already tracks), that's a compelling value proposition.

4. **Licensed data sources** would eliminate the ESPN API dependency risk. Sportradar, Stats Perform, or similar providers offer licensed feeds. This adds cost but removes existential risk.

5. **Community and social features** (the message board is a start) could create switching costs that free platforms lack. Private leagues with history, rivalry tracking, and social features build retention.

### 4.6 Portfolio Value

Evaluated as what it actually is -- a hobby project and portfolio piece -- sportDeets is exceptionally strong. It demonstrates:

- **Distributed systems design**: Event-driven pipeline with outbox guarantees, message bus abstraction (RabbitMQ/Azure Service Bus), and multi-service communication via typed HTTP clients.
- **Domain modeling at scale**: 130+ entities with external ID reconciliation, sport-specific polymorphism, and a canonical data model that normalizes messy external API data.
- **Production operations**: Self-hosted Kubernetes on bare metal, OpenTelemetry observability, health checks, CI/CD pipelines, and hybrid cloud infrastructure (on-prem compute + Azure managed services).
- **Full-stack delivery**: .NET backend through React frontend, with real-time updates (SignalR), authentication (Firebase), and a polished user experience.
- **Architectural judgment**: The modular monolith approach with extraction readiness is a pragmatic choice for a solo developer. The document processor framework is genuinely extensible without being over-abstracted.

A hiring manager reviewing this codebase would see someone who can design and operate complex systems end-to-end, not just write code for a single layer. The commercial headwinds described above are real but largely irrelevant to the project's actual purpose.
