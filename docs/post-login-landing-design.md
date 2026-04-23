# Post-login landing page — design whiteboard

Purpose of this doc: whiteboard the rules for what the logged-in landing page should
show, when, and why — so the static 2025 NCAAFB poll + CFB bracket can be replaced
with something that stays relevant as the calendar rolls. Reaction and redlines
expected; nothing here is decided.

---

## Goal

Answer a single question for the user within ~2 seconds of landing:

> **"Given who I am and when it is, what should I do or look at right now?"**

Everything else on the page is secondary to that answer.

---

## User segments

| Segment | Signal | Landing should prioritize |
|---|---|---|
| **New user** | No leagues, no followed teams | A single, obvious CTA: create or join a league. No filler. |
| **Active member, in-season** | ≥1 league in a sport whose season is currently running | Current week's picks state: deadline, pick record, standings delta |
| **Active member, off-season** | ≥1 league but none of their leagues' sports are active | Countdown to next kickoff + last season recap + "create 2026 league" CTA |
| **Multi-sport member** | ≥1 league across multiple sports, ≥1 in-season | Prioritize the in-season sport with the nearest deadline; surface others secondarily |
| **Commissioner-only** | Owns leagues but hasn't placed picks recently | Pending admin actions (invites, pending members, unset options) elevated above picks |

---

## Seasonal calendar (why date-awareness matters)

Rough bands for when each sport is "user-facing active" vs "background":

| Sport  | Jan  | Feb | Mar | Apr   | May | Jun | Jul | Aug  | Sep | Oct | Nov | Dec  |
|--------|------|-----|-----|-------|-----|-----|-----|------|-----|-----|-----|------|
| NCAAFB | CFP  | off | off | off   | off | off | off | camp | reg | reg | reg | bowl |
| NFL    | PO   | off | off | draft | off | off | off | camp | reg | reg | reg | reg  |
| MLB    | off  | ST  | ST  | reg   | reg | reg | reg | reg  | reg | PO  | off | off  |

Legend: **CFP** = College Football Playoff title (mid-Jan) · **PO** = playoffs ·
**off** = off-season · **camp** = training camp / preseason · **draft** = NFL Draft
(late April) · **ST** = spring training · **reg** = regular season · **bowl** = bowl
games (Dec, CFP semis leak into Jan).

NCAAFB note: kickoff weekend is **always the first weekend of September**; a handful
of "Week 0" games sometimes fall in the last week of August, but Aug is essentially
camp territory at month granularity.

Implication: **the "active sport" for a generic user rotates several times a year.**
The landing page can't hard-code one sport without going stale within months —
exactly what happened with the 2025 CFB bracket.

---

## Slot framework

Three-tier layout. Each tier fills independently; missing data in a tier
collapses it (doesn't push filler upward).

```
┌──────────────────────────────────────────────────────────────┐
│ TIER 1 — PRIMARY (above the fold, always present)            │
│  The next action the user should take.                       │
│  Examples: pick deadline for this week, standings delta      │
│  since last week, "create your 2026 league" for new users    │
├──────────────────────────────────────────────────────────────┤
│ TIER 2 — CONTEXT (1-2 cards, sport-specific)                 │
│  What's happening in the sport the user cares about most.    │
│  Examples: current week matchups, standings top 5,           │
│  Week N preview / AI prediction, key storyline               │
├──────────────────────────────────────────────────────────────┤
│ TIER 3 — SECONDARY (row of compact cards)                    │
│  Adjacent content. Date-appropriate filler if no signal.     │
│  Examples: other leagues' standings, league-invites pending, │
│  upcoming kickoff countdowns for sports not yet in-season    │
└──────────────────────────────────────────────────────────────┘
```

---

## Slot rules

### Tier 1 — Primary (the "next action")

Pick the first rule that matches, top-down:

1. **Pick deadline within next 48h in any active league** → "Picks due Sun 1pm — X matchups not set"
2. **New matchups became available since last visit** → "Your Week N matchups are ready"
3. **Standings change since last visit** → "You moved up 3 spots in [league]"
4. **Commissioner action pending** (invites, unset options, league incomplete) → "Your league needs attention"
5. **User has ≥1 league but it's all off-season** → "NCAAFB kicks off in N days — pick winners early"
6. **User has no leagues** → "Start your 2026 pick'em league" with sport selector
7. **Fallback** → "Welcome back — nothing urgent" + compact standings summary

### Tier 2 — Context

Two cards side-by-side (stack on mobile). Logic:

- **Left card**: active league with nearest deadline (or most-recent activity if no deadlines)
  - In-season: current week matchups + pick state
  - Off-season: last season recap + 2026 create/join CTA
- **Right card**: AI/insight surface — e.g., StatBot weekly preview, MetricBot confidence movers
  - Gated by whether previews are generated for the upcoming week
  - Falls back to a team/player spotlight if AI content isn't ready

### Tier 3 — Secondary (compact row)

4–6 small cards drawn from this pool, date-ranked:

| Card | Show when |
|---|---|
| Other leagues summary | User has ≥2 leagues |
| Pending invites | User has unaccepted league invites |
| Upcoming season countdown | A sport's kickoff is within 60 days |
| Current ranking snapshot | Sport is in-season and has rankings (NCAAFB AP poll) |
| Trending team watch | User follows a team and it has notable recent activity |
| "Discover public leagues" | User has <3 leagues |
| Historical recap | Off-season, shows last season's bracket/champion |

---

## Layout sketches for archetype users

### A. Active NCAAFB fan, mid-September 2026 (season just started)

```
┌──────────────────────────────────────────────────────────────┐
│ 🏈  Picks due Saturday 12pm  —  8 matchups pending  →       │
│     Saturday Showdown (your league)                          │
├────────────────────────────────────┬─────────────────────────┤
│ Week 3 Matchups                    │ StatBot's upset watch   │
│ • LSU @ Ole Miss        pick → 📋  │ 🔺 Virginia Tech +14    │
│ • Texas @ Arkansas      pick → 📋  │ 🔺 Indiana +17.5        │
│ • (etc)                            │                         │
├───────────┬───────────┬────────────┴─────────────────────────┤
│ You: 4-2  │ AP Top 25 │ Other league: 2nd of 8 (+1)           │
└───────────┴───────────┴──────────────────────────────────────┘
```

### B. User with only NFL leagues, mid-April 2026 (off-season)

```
┌──────────────────────────────────────────────────────────────┐
│ 🏈  NFL kicks off in 132 days                               │
│     Create your 2026 Sunday Funday league now →             │
├────────────────────────────────────┬─────────────────────────┤
│ Last season recap                  │ NFL Draft — Round 1     │
│ 🏆 Patriots won Sunday Funday      │ April 24 · 8pm ET       │
│ Your finish: 3rd of 10             │ Mock draft · StatBot    │
├───────────┬───────────┬────────────┴─────────────────────────┤
│ Discover  │ Invites   │ MLB is in-season — try a league?     │
│ leagues   │ (1)       │                                       │
└───────────┴───────────┴──────────────────────────────────────┘
```

### C. New user, any date

```
┌──────────────────────────────────────────────────────────────┐
│ 👋 Welcome to sportDeets                                    │
│                                                              │
│     Pick the 2026 season with friends                       │
│                                                              │
│     [ Create a league ]   [ Join with code ]                │
├──────────────────────────────────────────────────────────────┤
│ How it works · Example picks · What's StatBot?              │
└──────────────────────────────────────────────────────────────┘
```

---

## Open questions

1. **Time-zone handling for "deadline within 48h"** — user's timezone from profile, league's timezone, or matchup start time in UTC? Probably user's profile TZ for display, UTC for comparison.
2. **Signal persistence** — "since last visit" needs a last-seen timestamp per user. Do we have that, or do we need to store it?
3. **AI content gating** — Tier 2 right-card falls back if previews aren't generated. Should we generate eagerly on Sunday night for the following week, or lazy-fetch when the landing page asks?
4. **Multi-sport ordering** — when a user has an NFL league (Sunday picks) *and* an MLB league (daily picks), what's the tiebreaker for primary slot? Nearest deadline wins? Sport preference?
5. **Commissioner vs. member view** — do commissioners see a different Tier 1 than members, or is "your league needs attention" just another rule that outranks picks when applicable?
6. **Archival/recap depth** — should "last season recap" be just a champion card, or a richer historical page behind a link?
7. **Mobile-first implications** — the three-tier stack collapses fine vertically, but Tier 3's 4–6 cards need a scroll strategy on narrow screens. Horizontal scroll, or hide after N?

---

## Not-in-scope for v1

To keep the first iteration small, suggest deferring:

- Push/email notifications triggered from the same signals (separate channel, same rules engine)
- "Edit your landing page" customization (don't give users controls until defaults prove out)
- Cross-sport fantasy-style insights (requires a lot more data plumbing)
- Social feed / friends' picks surfaced on landing (privacy + content-moderation concerns)
