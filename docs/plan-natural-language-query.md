# Plan: Natural Language Query Feature

## Vision

Allow users to ask questions about sports data in natural language and receive accurate, data-driven responses. This is the consumer-facing evolution of the StatBot/MetricBot AI capabilities — moving from pre-generated narratives to on-demand, interactive querying.

### Example Queries
- "Who was the rushing leader in the SEC for the 2025 season?"
- "What is the average margin of victory between LSU and Alabama for games in Tuscaloosa since 2000?"
- "How many home runs has Aaron Judge hit in April across his career?"
- "What team has the best road record in the AL East this season?"
- "Show me the win probability chart for last night's Yankees game"
- "Which pitchers have the lowest ERA against left-handed batters this season?"

## Why This Matters

The canonical data layer IS the moat. 20+ years of NCAA football, growing NFL and MLB data, all normalized and queryable. The natural language interface transforms that data asset from "powering internal features" to "directly accessible to users" — which is the differentiator no competitor has.

Current-season-only apps can answer "what happened last night." sportDeets can answer "how does last night compare to the last 20 years of games in this matchup."

## Architecture Options

### Option A: LLM + Schema Context → SQL Generation

Give an LLM (Claude API) the database schema and let it generate SQL queries.

**Flow:**
1. User submits question via chat UI
2. API sends question + schema context + example pairs to Claude
3. Claude generates SQL query
4. API validates query (read-only, timeout, result limit)
5. API executes query against read-only PostgreSQL replica/connection
6. API formats results and sends back to user
7. Optionally: Claude generates a natural language summary of the results

**Pros:**
- Highly flexible — handles novel questions
- Schema context can be sport-specific (only show baseball tables for baseball questions)
- Few-shot examples improve accuracy dramatically
- Can generate complex joins, aggregations, window functions

**Cons:**
- Hallucination risk on complex queries
- LLM needs to understand sportDeets schema conventions (FranchiseSeason vs Franchise, SeasonYear scoping, canonical ID patterns)
- Cost per query (Claude API tokens)
- Latency (LLM generation + query execution)

### Option B: Semantic Layer / Predefined Query Templates

Map common question patterns to parameterized queries. Use LLM only for intent classification and slot extraction.

**Flow:**
1. User submits question
2. LLM classifies intent (e.g., "leader_by_stat", "head_to_head_record", "team_record_filter")
3. LLM extracts slots (sport, conference, team, season, stat, location, etc.)
4. System maps intent + slots to a predefined SQL template
5. Execute and return results

**Pros:**
- Predictable, tested queries — no hallucination
- Faster (LLM only does classification, not generation)
- Cheaper (smaller prompts)
- Can be cached aggressively

**Cons:**
- Limited to predefined templates — can't answer truly novel questions
- Requires building and maintaining a template library
- "Sorry, I can't answer that" for unsupported patterns

### Option C: Hybrid (Recommended)

Start with Option B for common patterns (stat leaders, head-to-head, records by filter) and fall back to Option A for questions that don't match a template. Track which Option A queries succeed and use them to build new templates over time.

**Flow:**
1. User submits question
2. LLM classifies intent and extracts slots
3. If intent matches a template → execute template (fast, reliable)
4. If no template match → generate SQL via Option A (slower, less reliable)
5. Log all generated SQL for review and potential template promotion

## Technical Requirements

### Read-Only Data Access
- Dedicated read-only PostgreSQL connection string (or read replica)
- Query timeout (5-10 seconds max)
- Result size limit (1000 rows max)
- No DDL, no DML — SELECT only
- Connection pool isolated from Producer/Provider workloads

### Schema Context Management
- Curated schema descriptions per sport (not raw DDL — annotated with business meaning)
- Example: `FranchiseSeason` = "A team's identity for a specific year (name, logo, conference, record)"
- Relationship descriptions: "Contest.HomeTeamFranchiseSeasonId → FranchiseSeason → Franchise"
- Sport-scoped: baseball questions only see baseball-relevant tables

### LLM Integration
- Claude API (Anthropic SDK) — already in the stack for game recaps
- System prompt with schema context, conventions, and guardrails
- Few-shot examples per sport (10-20 curated question → SQL pairs)
- Temperature 0 for SQL generation (deterministic)
- Streaming response for conversational feel

### Chat UI
- Chat component in web app (React) and mobile app (React Native)
- Conversation history within session
- Sport context inherited from current view
- Result rendering: tables, single values, charts (reuse existing chart components)
- "How did you get this?" link showing the generated SQL (transparency)

### Safety & Guardrails
- SQL injection prevention: parameterized queries where possible, query validation layer
- Rate limiting per user (prevent abuse / cost runaway)
- Query complexity scoring (reject queries with too many joins or full table scans)
- PII awareness: no user data exposed through queries (sports data only)
- Cost tracking per query for subscription tier enforcement

## Schema Readiness

### Already Query-Friendly
- Franchise + FranchiseSeason (team identity by year)
- Contest (games with scores, dates, venues, week numbers)
- Competition + CompetitionPlay (play-by-play)
- Athlete + AthleteSeason (player data by year)
- GroupSeason (conferences/divisions)
- SeasonWeek (week boundaries)
- FranchiseSeasonRecord (win/loss records)
- FranchiseSeasonStatistic (team stats)
- CompetitionLeader (game leaders by category)

### Gaps to Fill
- No materialized views for common aggregations (season leaders, head-to-head records)
- No full-text search on athlete/team names (fuzzy matching for "Bama" → Alabama)
- Cross-sport queries would need a unified view layer
- Historical data completeness varies by sport and year

## Phasing

### Phase 1: Foundation
- Create read-only PostgreSQL connection/role
- Build schema context documents per sport
- Curate 20 example question → SQL pairs per sport
- Build API endpoint: `POST /api/query` accepting natural language
- Basic Claude integration with SQL generation
- Simple result formatting (JSON table)

### Phase 2: Chat UI
- Chat component in web app
- Conversation history (session-scoped)
- Result rendering (tables, single values)
- Sport context from current view
- Error handling ("I couldn't find an answer" vs "query failed")

### Phase 3: Templates
- Analyze query logs for common patterns
- Build parameterized query templates for top 20 patterns
- Intent classification layer
- Template execution path (bypasses LLM SQL generation)

### Phase 4: Polish
- Streaming responses
- Chart rendering for time-series and comparison queries
- "How did you get this?" transparency
- Subscription gating for premium query features
- Mobile app chat component
- Query caching for repeated questions

## Pre-Game Context Queries (Static / Scheduled)

In addition to on-demand user queries, a curated set of pre-defined queries will run automatically before each contest to generate the "analyst desk" context package — the kind of information commentators discuss during pre-game shows.

### Use Cases
- **StatBot matchup previews**: narrative-style pre-game analysis powered by structured query results
- **MetricBot prediction inputs**: feature vectors for the ML prediction models (logistic regression SU, random forest ATS)
- **Contest overview UI enrichment**: contextual data displayed alongside odds, lineups, and recent scores

### Example Pre-Game Queries
- Series history: "All-time record between these two franchises, overall and at this venue"
- Recent form: "Each team's last 10 games — record, average score, margin of victory"
- Venue performance: "Home team's record at this venue over the last 3 seasons"
- Head-to-head scoring trends: "Average combined score in this matchup over the last 5 meetings"
- Key player matchups: "Starting pitcher's career stats against this opponent's active roster"
- Conference/division standing context: "Where each team sits in the standings and games back"
- Historical spread performance: "How each team has performed ATS as home/away favorites/underdogs this season"
- Weather/venue factors: "Indoor vs outdoor, grass vs turf, day vs night game historical splits"

### Architecture
- Pre-defined SQL queries stored as templates (not LLM-generated — these are tested and trusted)
- Scheduled via Hangfire: run N hours before first pitch (configurable per sport)
- Results stored as JSON on the Contest entity or a related `ContestContext` table
- StatBot consumes the structured results to generate narrative previews
- MetricBot consumes the same results as model feature inputs
- UI displays relevant context cards on the contest overview page

### Live In-Game Contextual Insights

Beyond pre-game context, the same query infrastructure powers real-time "announcer-style" commentary during live games. Play events flowing through the `CompetitionPlayCompleted` pipeline trigger contextual lookups that produce insights pushed to connected clients via SignalR.

**Examples:**
- Penalty event → "That's the second game this season where #72 has been flagged for multiple holding penalties"
- Home run event → "Judge now has 5 home runs in April — his best April start since 2022"
- Strikeout event → "Cole has struck out 8 tonight — his highest total since June 14 vs. Boston"
- Scoring play → "This is the first time the Guardians have trailed by more than 3 runs at home this season"
- Game situation → "Teams trailing by 2+ runs in the 7th inning or later have won only 18% of games this season"

**Architecture:**
- `CompetitionPlayCompleted` event triggers an insight evaluation pipeline
- Pipeline checks the play against a library of insight templates (penalty count thresholds, milestone checks, streak detection, historical comparisons)
- Each template is a parameterized query against canonical data with a narrative template attached
- Matching insights are published as `LiveInsightGenerated` events
- SignalR hub pushes insights to connected clients viewing that contest
- Insights are also persisted for post-game recap generation

**Key Challenge:**
Latency. The insight must be generated and delivered within seconds of the play, not minutes. This favors pre-cached player/team context loaded at game start (penalty counts, season stats, milestone proximity) over real-time SQL queries. The play event updates the in-memory context and checks thresholds — no database round-trip needed for most insights.

**Phasing:**
- Phase 1: Score change notifications via SignalR (current MLB proving ground)
- Phase 2: Game state changes (lead changes, final scores, extra innings)
- Phase 3: Statistical milestone detection (round numbers, season highs, streaks)
- Phase 4: Historical context enrichment (head-to-head, venue, situational)
- Phase 5: LLM-generated narrative wrapping ("announcer voice" on top of structured insights)

### Relationship to On-Demand Queries
The pre-game templates are the seed library for the template-based query path (Option B). As users ask similar questions interactively, the system already has the infrastructure to answer them instantly from cached pre-game results. Over time, the pre-game query library and the interactive query templates converge — same queries, different triggers (scheduled vs on-demand).

## Key Decisions (TBD)

1. **Which LLM?** Claude (already integrated) vs OpenAI vs local (Ollama is deployed). Cost vs latency vs accuracy tradeoff.
2. **Read replica or read-only connection?** Replica adds infra complexity but isolates query workload completely.
3. **Per-sport databases or cross-sport queries?** Current architecture has separate databases per sport. Cross-sport queries ("compare MLB vs NFL attendance") would need a federation layer.
4. **Subscription tier?** Free users get N queries/day, premium unlimited? Or free for simple, premium for complex?
5. **Caching strategy?** Same question asked by different users should return cached result. Cache key = normalized question + sport context.
