# Contest API Design

## Problem Statement

ESPN uses a `Contest/Competition[]` pattern, but empirically contests never have more than one competition across all observed sports. This creates unnecessary complexity and naming confusion.

**Decision**: Use **Contest** as the primary domain boundary and phase out "Competition" terminology.

---

## Domain Model Simplification

### Current ESPN Model
```
Contest (event wrapper)
  └── Competition[] (actual game/match)
      ├── Competitors[]
      ├── Scores
      ├── Status
      └── Venue
```

### Proposed Simplified Model
```
Contest (the game/match itself)
  ├── Competitors[]
  ├── Scores
  ├── Status
  ├── Venue
  └── SeasonYear
```

**Rationale**: 
- Eliminates unnecessary nesting
- Clearer naming (a "contest" is the game)
- Aligns with common sports terminology
- Simplifies queries (no joins through intermediate Competition table)

---

## URL Pattern Options

### Option 1: Year-Based Collection
```
GET /api/football/ncaa/contests?year=2025
GET /api/football/ncaa/contests?year=2025&week=1
GET /api/football/ncaa/contests/{contestId}
```

**Pros:**
- Simple, flat structure
- Easy filtering by year/week
- Clear primary resource

**Cons:**
- Doesn't reflect franchise relationship
- Can't discover "all LSU games" via HATEOAS from franchise

---

### Option 2: Franchise-Nested with Year
```
GET /api/football/ncaa/franchises/lsu-tigers/contests?year=2025
GET /api/football/ncaa/franchises/lsu-tigers/contests/{contestId}
GET /api/football/ncaa/contests/{contestId}  (direct access)
```

**Pros:**
- HATEOAS-friendly (franchise links to contests)
- Natural "view all games for team" pattern
- Year as query param (flexible filtering)

**Cons:**
- Requires franchise lookup first
- Doesn't work for "all games in week 1"

---

### Option 3: Dual Access Patterns with SeasonWeek Hierarchy (RECOMMENDED)
```
# Global contest access (query params for filtering)
GET /api/football/ncaa/contests?year=2025&week=1
GET /api/football/ncaa/contests/{contestSlug}

# Season-scoped access
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests?week=1

# Week-scoped access (SeasonWeek as resource container)
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/{weekNumber}
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/{weekNumber}/contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/{weekNumber}/contests/{contestSlug}
```

**Pros:**
- SeasonWeek treated as proper aggregate root
- Week endpoint can return metadata (dates, phase, standings)
- RESTful hierarchy: Franchise → Season → SeasonWeek → Contests
- HATEOAS-navigable from multiple entry points
- Supports both direct season contests and week-scoped contests

**Cons:**
- More endpoint variations to maintain
- Slightly deeper nesting for week-scoped access

**Cons:**
- More endpoint variations to maintain
- Slightly deeper nesting for week-scoped access

---

## Path vs Query Parameter Decision Criteria

### When to Use Path Parameters
**Path parameters identify resources or resource containers:**
- The parameter identifies **which specific resource** you're accessing
- Removing it would make the URL invalid or point to a different resource type
- It represents a **domain aggregate** with its own identity and lifecycle
- It's **required** to define what collection you're viewing

**Examples:**
- `/franchises/{franchiseSlug}` - slug identifies THE franchise
- `/seasons/{year}` - year identifies THE season
- `/weeks/{weekNumber}` - number identifies THE week (SeasonWeek aggregate)
- `/contests/{contestSlug}` - slug identifies THE contest

### When to Use Query Parameters
**Query parameters filter, sort, or paginate collections:**
- The parameter is **optional refinement** of a collection
- Removing it returns **more results**, not a different resource
- It doesn't represent a domain aggregate, just a filter criterion
- Multiple filters can be combined: `?homeGame=true&opponent=alabama`

**Examples:**
- `?sort=date&order=desc` - sorting options
- `?page=2&pageSize=20` - pagination
- `?homeGame=true` - filter by home/away
- `?opponent=alabama` - filter by opponent

### The SeasonWeek Conundrum
**SeasonWeek is an aggregate root in the domain model:**
- Has its own table, GUID identity, external IDs
- Contains metadata: StartDate, EndDate, SeasonPhase, IsNonStandardWeek
- Can be represented as a resource with its own properties
- Contests belong to a SeasonWeek, not just "have a week number"

**Decision: Use path parameter `/weeks/{weekNumber}`**
- SeasonWeek is a **resource container**, not a filter
- Allows SeasonWeek to have its own endpoint: `GET /weeks/1` returns week metadata
- RESTful hierarchy: Season → SeasonWeek → Contests
- More discoverable via HATEOAS (season links to weeks, week links to contests)

---

## Recommended Approach: Option 3 (Dual Access)

### Primary Access Patterns

#### 1. Global Contest Queries (Query Params for Filtering)
```
GET /api/football/ncaa/contests?year=2025&week=1&top=50
GET /api/football/ncaa/contests?date=2025-09-01
GET /api/football/ncaa/contests/{contestSlug}
```

**Note:** Global endpoint uses query params because it's filtering across all contests, not navigating a resource hierarchy.

**Response includes:**
```json
{
  "id": "...",
  "slug": "lsu-vs-alabama-2025-11-09",
  "dateTime": "2025-11-09T19:00:00Z",
  "status": "completed",
  "homeTeam": { ... },
  "awayTeam": { ... },
  "ref": "/api/football/ncaa/contests/lsu-vs-alabama-2025-11-09",
  "links": {
    "self": "/api/football/ncaa/contests/lsu-vs-alabama-2025-11-09",
    "homeTeam": "/api/football/ncaa/franchises/lsu-tigers",
    "awayTeam": "/api/football/ncaa/franchises/alabama-crimson-tide",
    "venue": "/api/football/ncaa/venues/tiger-stadium"
  }
}
```

#### 2. Season-Scoped Queries (All Franchise Contests)
```
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests?week=1
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests/{contestSlug}
```

**Benefits:**
- Natural navigation: franchise → season → contests
- Query param for week filtering (optional refinement)
- HATEOAS link on season: `links.contests`

#### 3. Week-Scoped Queries (SeasonWeek as Aggregate)
```
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/1
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/1/contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/1/contests/{contestSlug}
```

**Benefits:**
- SeasonWeek is a proper resource with metadata (StartDate, EndDate, SeasonPhase)
- Week endpoint can include standings, rankings, statistics
- RESTful hierarchy: franchise → season → week → contests
- HATEOAS link on week: `links.contests`
- Natural user journey: "Show me Week 1" → "Show me Week 1 games"

#### 3. Direct Contest Access
```
GET /api/football/ncaa/contests/{contestSlug}
```

**Slug Pattern**: `{home-slug}-vs-{away-slug}-{date}`
- Example: `lsu-tigers-vs-alabama-crimson-tide-2025-11-09`
- Human-readable
- Sortable by date suffix
- **Note**: Doubleheaders require sequence suffix (see below)

---

## HATEOAS Integration

### Franchise Response Enhancement
```json
{
  "slug": "lsu-tigers",
  "displayName": "LSU Tigers",
  "links": {
    "self": "/api/football/ncaa/franchises/lsu-tigers",
    "seasons": "/api/football/ncaa/franchises/lsu-tigers/seasons"
  }
}
```

**Note**: Contests accessed through season, not directly from franchise

### Season Response Enhancement
```json
{
  "slug": "lsu-tigers",
  "year": 2025,
  "links": {
    "self": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025",
    "franchise": "/api/football/ncaa/franchises/lsu-tigers",
    "weeks": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks",
    "contests": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests"
  }
}
```

### SeasonWeek Response (NEW)
```json
{
  "number": 1,
  "startDate": "2025-08-30",
  "endDate": "2025-09-06",
  "seasonPhase": "Regular Season",
  "isNonStandardWeek": false,
  "links": {
    "self": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/1",
    "season": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025",
    "contests": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/1/contests",
    "next": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/2",
    "prev": null
  }
}
```

### Contest Collection Response
```json
{
  "items": [ ... ],
  "totalCount": 12,
  "filters": {
    "year": 2025,
    "franchiseSlug": "lsu-tigers"
  },
  "links": {
    "self": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests",
    "franchise": "/api/football/ncaa/franchises/lsu-tigers",
    "season": "/api/football/ncaa/franchises/lsu-tigers/seasons/2025",
    "next": null
  }
}
```

---

## Pattern Usage Guidelines

### When to Use Week-Scoped vs Season-Scoped

**Use Week-Scoped (`/weeks/{weekNumber}/contests`):**
- User navigating week-by-week ("Next Week", "Previous Week")
- Displaying week metadata alongside contests (dates, phase, standings)
- Week-centric UI (weekly schedule view)
- HATEOAS navigation from week resource

**Use Season-Scoped with Filter (`/seasons/{year}/contests?week=1`):**
- Direct access when week number is already known
- Combining with other filters: `?week=1&homeGame=true`
- Backend queries where week metadata not needed
- Simpler client code when just filtering contests

**Both are valid** - choose based on use case. Week-scoped treats SeasonWeek as a first-class resource; season-scoped treats week as a filter.

---

## Producer Layer Design

### Producer Endpoints (GUID-based)
```
GET /api/contests?year=2025&week=1&pageNumber=1&pageSize=50
GET /api/contests/{contestId}
GET /api/franchises/{franchiseId}/contests?year=2025
```

**Key Differences:**
- Producer uses GUIDs for performance
- API resolves slugs → GUIDs before calling Producer
- Producer returns canonical `ContestDto`

---

## Contest Slug Strategy

### Slug Generation Logic
```
{homeTeamSlug}-vs-{awayTeamSlug}-{date}[-{gameNumber}]
```

**Examples:**
- `lsu-tigers-vs-alabama-crimson-tide-2025-11-09` (single game)
- `yankees-vs-red-sox-2025-07-04-1` (doubleheader game 1)
- `yankees-vs-red-sox-2025-07-04-2` (doubleheader game 2)
- `georgia-bulldogs-vs-texas-longhorns-2026-01-20` (championship)

**Doubleheader Handling:**
- **MLB/Baseball**: Doubleheaders are common (makeup games, traditional doubleheaders)
- **Other Sports**: Rare but possible (neutral site tournaments, makeup games)
- **Logic**: Check for existing contest with same teams + date, append `-{gameNumber}` if found
- **Game Number**: 1-indexed (first game gets `-1`, second gets `-2`, etc.)

**Slug Storage:**
- Add `Slug` column to `Contest` entity
- Generate on creation
- Index for fast lookups

---

## Query Patterns

### Common User Journeys

#### 1. "Show me LSU's 2025 season"
```
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025
  → Follow links.contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests
  → Returns all LSU games for 2025
```

#### 2. "What games are on this week?"
```
GET /api/football/ncaa/contests?year=2025&week=14
  → Returns all games in week 14
```

#### 3. "Show me the LSU vs Alabama game"
```
GET /api/football/ncaa/contests/lsu-tigers-vs-alabama-crimson-tide-2025-11-09
  → Direct access via slug
```

#### 4. "Show me all LSU 2024 games"
```
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2024
  → Follow links.contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2024/contests
  → Returns all LSU games for 2024
```

#### 5. "Show me Week 1 games for LSU"
```
# Option A: Week-scoped (if browsing week metadata)
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025
  → Follow links.weeks
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks
  → Returns week collection, follow week 1 link
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/1
  → Returns week metadata, follow links.contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/weeks/1/contests
  → Returns LSU's Week 1 game(s)

# Option B: Season-scoped with filter (direct access)
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2025/contests?week=1
  → Returns LSU's Week 1 game(s) directly
```

#### 6. "All Week 1 scores across the league"
```
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2024
  → Follow links.contests
GET /api/football/ncaa/franchises/lsu-tigers/seasons/2024/contests
  → Returns all LSU games for 2024
```

---

## Migration Strategy

### Phase 1: Dual Naming (Transitional)
- Keep existing "Competition" code paths working
- Add new "Contest" endpoints alongside
- Both return same data, different routes
- Deprecation warnings on Competition endpoints

### Phase 2: Internal Refactoring
- Rename `Competition` entity → `Contest`
- Update all queries/commands to use Contest terminology
- Update database tables (migrations)
- Keep old API routes proxying to new implementation

### Phase 3: API Cutover
- Remove deprecated Competition endpoints
- Full contest-based API
- Update documentation

---

## Open Questions

### 1. Contest vs Competition in Database?
**Options:**
- A. Rename `Competition` table → `Contest` (breaking change for existing data)
- B. Keep `Competition` table, use `Contest` in API/services (mapping layer)
- C. Create new `Contest` table, migrate data, deprecate `Competition`

**Recommendation**: Option C - safest migration path, allows gradual rollout

### 2. How to Handle Neutral Site Games?
**Scenario**: LSU vs Wisconsin at Lambeau Field (Green Bay)
- Home team designation may be arbitrary
- Slug still works: `lsu-tigers-vs-wisconsin-badgers-2025-09-03`
- `homeTeam` field may have `isNeutralSite: true`

### 3. Playoff/Championship Game Slugs?
**Examples:**
- CFP Semifinal: `georgia-bulldogs-vs-ohio-state-buckeyes-2026-01-01`
- National Championship: `georgia-bulldogs-vs-texas-longhorns-2026-01-20`

**No special handling needed** - slug pattern works universally

### 4. Query Performance for Franchise Contests?
**Concern**: `/franchises/{id}/contests` may scan all contests

**Solution**: Index strategy
```sql
CREATE INDEX idx_contests_franchise_year 
ON contests (home_team_franchise_id, season_year);

CREATE INDEX idx_contests_franchise_year_away 
ON contests (away_team_franchise_id, season_year);
```

Query uses `WHERE (home_team_franchise_id = ? OR away_team_franchise_id = ?) AND season_year = ?`

---

## Next Steps

1. **Design Review**: Validate slug strategy and URL patterns
2. **Entity Design**: Create `Contest` entity with proper indexes
3. **Producer Endpoints**: Implement GUID-based contest queries
4. **Client Layer**: Create `ContestClient` + `ContestClientFactory`
5. **API Layer**: Implement slug-based contest endpoints with HATEOAS
6. **Migration Plan**: Define Competition → Contest transition strategy

---

## Related Considerations

### SeasonWeek Relationship
- Contests belong to a SeasonWeek
- Need `/api/football/ncaa/seasons/2025/weeks/14/contests`?
- Or keep week as query param: `?week=14`

**Recommendation**: Query param for simplicity
- Weeks are filter criteria, not aggregate roots
- Avoid deep nesting: `/seasons/{year}/weeks/{week}/contests`

### Live Contest Updates
- Real-time score updates via WebSocket/SSE?
- HATEOAS can include `links.liveUpdates` when game is in progress
- Separate live-data endpoint: `/api/football/ncaa/contests/{slug}/live`

### Competitor Details
- Should competitors be expanded inline or linked?
- Option A: Full competitor details inline (simpler client)
- Option B: Links only, client fetches separately (smaller payload)

**Recommendation**: Hybrid
- Include basic competitor info inline (name, slug, score)
- Provide `links.homeTeamDetails` for full franchise data

---

## Summary

**Recommended Architecture:**
- **Domain**: Contest is primary aggregate root (no Competition)
- **URLs**: Dual access (global + franchise-scoped)
- **Slugs**: `{home}-vs-{away}-{date}` pattern
- **HATEOAS**: Rich linking from franchise/season to contests
- **Migration**: Gradual (new Contest alongside old Competition)

This design balances flexibility, discoverability, and RESTful principles while maintaining performance through GUID-based internal operations.
