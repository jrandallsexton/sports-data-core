# Game Map (`/app/map`) — hardening roadmap

Working notes for evolving the league-week game map (`src/UI/sd-ui/src/components/map/GameMap.jsx`) from "played with last year"
prototype into a first-class product surface for the upcoming season.
The page already plots Google Maps markers at venues for every contest
in the user's selected league + week — data plumbing works, base UX
does not.

> **Already pick'em-scoped.** The top-left selector locks `League` +
> `Week`. The handler that powers the map
> (`SportsData.Api/Application/UI/Map/Queries/GetMapMatchups/`) was
> recently fixed (PR #451) to honor `league.Sport` instead of hardcoded
> `FootballNcaa` — so the markers reflect the league's actual sport.
> The data layer knows which league it's serving; the visualization
> layer hasn't been told.

## Current state — what exists

- **Map page**: `src/UI/sd-ui/src/components/map/GameMap.jsx`. Uses
  `@react-google-maps/api`'s `useLoadScript` hook (CSP hash for the
  inline loader landed in PR #450 — see
  `src/UI/sd-ui/security-headers.conf`).
- **API endpoint**: backed by `GetMapMatchupsQueryHandler`. Resolves the
  selected league's sport, hits Producer via `IContestClientFactory` for
  matchups in the week.
- **Marker rendering**: red `SymbolPath.CIRCLE` per venue, colored only
  by status legend (Upcoming / Live / Final) — but the screenshots
  prove the differentiation isn't actually delivered visually.
- **InfoWindow on click**: card with venue name, city/state, game
  date/time, matchup string with W-L records, status badge, and a
  Spread line.

## Pain points (visible in screenshots)

1. **Marker collision is severe in dense regions.** MLB Week 13: NYC /
   Cleveland / Mid-Atlantic stack 4+ markers on top of each other; both
   the compact and "Hide All" expanded modes are unreadable there.
   Pure pixel-level density problem.
2. **All three statuses render identically.** Legend at the top right
   implies Upcoming / Live / Final are distinguishable; on the actual
   map they're not. The status data is on the DTO but the marker
   renderer ignores it.
3. **InfoWindow is anemic.** Has venue + records + status badge + spread
   line. Missing the things that make this *sportdeets*:
   - **Final score** for completed games (says "Final" with no score)
   - **Your pick + outcome** ("You picked SEA -1.5 → Won")
   - **Provider attribution** on the spread (PR #448 added `ProviderName`
     to the same league-week DTO surface; map InfoWindow doesn't read it)
   - **Team logos** (`sportdeets-mark` roundels already on
     `FranchiseLogo`)
   - **Click-through** to the contest detail / play log — popup is a
     dead end today
   - **Spread resolution** for Final games (`-1.5` should resolve to
     "SEA covered" / "BOS covered" / "push", not just echo the line)
4. **Generic Maps base layer fights the dark app chrome.** Default
   green-topography theme; markers don't pop against it.
5. **No pick'em context anywhere on the map itself.** This is the
   strategic miss. We've already paid for league scope at the data layer
   — the markers don't differentiate "your picks" vs. "other games"
   vs. "games where the outcome locks a leaderboard delta for your
   league."
6. **Stale hover-tooltip lingering bug.** Visible in the screenshot:
   marker click opens an InfoWindow while the hover-state tooltip from
   the marker doesn't clear. Separate small fix.

## Opportunities, ranked by impact-to-effort

### 1. InfoWindow enrichment — *highest ROI*

**Why first**: half-day of work, no Maps API surgery, immediate user
value, the data sources already exist on the same wire surface that
powers `MatchupCard`. Pure UI / DTO threading.

**Specifically**:
- Show final score for Final games
- Surface user's pick for this contest (lookup vs. existing
  `UserPicks` table — same data the picks page uses) and the
  win/loss/push outcome on Final games
- Add `ProviderName` to the spread line (already on
  `LeagueMatchupDto` after PR #448)
- Add team logos (we have `sportdeets-mark` roundels on
  `FranchiseLogo`)
- Click-through to `/app/contests/{contestId}` overview page
- Resolve spread for Final games (compare final score against the
  line, render "SEA covered" / "BOS covered" / "push")

### 2. Marker clustering

**Why second**: solves the dense-region readability problem
fundamentally — without this, every other improvement is fighting the
collision noise.

**Specifically**:
- `MarkerClustererF` from `@react-google-maps/api`, or standalone
  `@googlemaps/markerclusterer`. Both are well-trodden React patterns.
- Cluster count badge shows total contests at that zoom level. Custom
  badge shape/color (sportdeets brand) — not the default red blob.
- Click cluster → smooth zoom-in to expose the underlying markers.

### 3. Status-driven marker visuals

**Why third**: cheap one-evening change with outsized clarity gain.
Status is already on the DTO; we just need to render it.

**Specifically**:
- **Upcoming**: outlined circle (no fill), team-neutral
- **Live**: pulsing circle (CSS animation or animated SVG), brighter
  saturation, optional inline mini score
- **Final**: filled circle in the *winner's* team primary color (so
  the map at-a-glance answers "who won where")

### 4. Custom dark base map

**Why fourth**: Google Maps `styles` JSON option, paste in a dark theme
that matches the app chrome (`#0d1117`-adjacent), markers immediately
pop. Standard pattern; design-token sized PR.

### 5. Pick'em overlay on markers themselves

**Why last**: builds on #1–#4 — once status visuals + custom base +
clean clustering are in place, pick'em-specific decoration has a clean
substrate to render against. Without that foundation, layering pick
context on top of a chaotic map just adds noise.

**Specifically**:
- Glow ring around markers for contests you've picked
- Special treatment for "outcome-locks-a-leaderboard-delta" contests
  (color of the ring shifts to indicate magnitude of expected
  leaderboard movement)
- Aggregate consensus chip in the InfoWindow ("8/12 in your league
  picked SEA")
- Filter chip: "Only games I've picked"

## Sequence

PR-by-PR, in order:

1. **PR — InfoWindow v2.** Final score, your pick + outcome, provider
   attribution, team logos, click-through, spread resolution. Plus the
   hover-tooltip lingering bug fix.
2. **PR — Marker clustering.** Library integration, custom cluster
   badge, smooth zoom-in.
3. **PR — Status-driven visuals.** Upcoming outline, Live pulsing,
   Final winner-color fill.
4. **PR — Custom dark base map.** Maps `styles` JSON, color tokens
   pulled from app theme.
5. **PR — Pick'em overlay.** Pick ring, league-consensus chip, "my
   picks only" filter chip.

Each PR is independently shippable; #5 strictly depends on #1 sharing
its pick lookup. #2, #3, #4 are independent and can swap order if
prioritization shifts.

## Out of scope / future considerations

- **Live score push to InfoWindow content** — the picks page wires up
  to `ContestUpdatesContext` for `*PlayCompleted` events; the map could
  reuse the same context so an open InfoWindow on a live game updates
  score in real time. Worth a follow-up PR once #1 + #3 are in.
- **Command Center crossover** — per
  `memory/project_command_center_vision.md`, the multi-game wallboard
  vision could plausibly overlay or attach to map markers as a
  "watch live games" entry point. Don't design for this yet, but keep
  the door open in #5's pick-context architecture (don't bake
  assumptions that block it later).
- **Geo-aware marketing surface (about/landing)** — the same map
  visualization without league scope is a compelling marquee for the
  about site. Out of scope for this roadmap; the league-scoped variant
  is the priority.
- **Mobile parity** — sd-mobile doesn't have a map yet. Once web is
  hardened, port. Not on this roadmap.

## Touch points to know about

- `GameMap.jsx` — main component, marker rendering, InfoWindow content
- `GetMapMatchupsQueryHandler.cs` — API surface, returns matchups for
  the selected league-week
- `LeagueMatchupDto.cs` — wire DTO; already carries `ProviderName`,
  `SpreadCurrent`, scores, winner IDs
- `security-headers.conf` — CSP. If `@react-google-maps/api` bumps and
  the inline loader hash invalidates, recover per the comment block
  there.
- `ContestUpdatesContext.js` — live event hookup, reusable for
  InfoWindow live updates in the future
