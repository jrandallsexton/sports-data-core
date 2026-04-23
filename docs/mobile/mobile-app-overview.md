# Mobile App Overview — SportDeets (sd-mobile)

## Context

The mobile app is the Year 2 priority for the platform. The web app (`sd-ui`) has been the primary UI, but the real engagement surface for a Pick'em product is mobile — nobody opens a browser on Saturday morning to make picks. The goal is to have a functional mobile app in people's pockets before the 2026 NCAAFB season kicks off (first weekend of September).

**Timeline**: Target August 2026 for beta-ready. ~3.5 months from today (April 22, 2026). The web app has pulled ahead on multi-sport support — mobile needs to close that gap plus ship the must-haves below before beta.

## Technology Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Framework | Expo SDK 55 / React Native 0.83.2 | Abstracts native toolchain pain; no Xcode/Android Studio needed on Windows dev machine |
| Routing | Expo Router (file-based) | Convention-over-configuration, like Next.js |
| State | Zustand (auth) + React Query (server state) | Lighter than Redux; React Query handles caching/invalidation natively |
| Auth | Firebase (email/password) | Already in place for web; well-worn path with Expo |
| HTTP | Axios with Firebase JWT interceptor | Same backend API as web app |
| Forms | react-hook-form + zod | Type-safe validation |
| Build/Deploy | EAS (Expo Application Services) | Cloud builds for iOS without a Mac; CI via GitHub Actions |
| Testing | Jest 29 + jest-expo + @testing-library/react-native | Jest 30 incompatible with Expo 55 |
| Platforms | iOS and Android | Single codebase via Expo |
| Workflow | Managed (not bare) | Simpler; can eject later if needed |

## Current App Structure

```
sd-mobile/
├── app/                          # Expo Router
│   ├── _layout.tsx               # Root: QueryClient + AuthGuard
│   ├── (auth)/
│   │   └── sign-in.tsx           # Login screen
│   ├── create-league.tsx         # League creation (Phase 1 NCAA-only scaffold)
│   └── (tabs)/
│       ├── index.tsx             # Home (pick record + standings preview)
│       ├── picks.tsx             # Matchup list with league/week selector
│       ├── standings.tsx         # Leaderboard for selected league
│       ├── profile.tsx           # User profile + sign-out
│       └── (details)/
│           └── sport/
│               └── [sport]/
│                   └── [league]/
│                       ├── game/[id].tsx     # Contest overview (sport-aware)
│                       └── team/[slug].tsx   # Team card (sport-aware)
├── src/
│   ├── components/
│   │   ├── ui/                   # Button, Card, LoadingSpinner, EmptyState, SegmentedControl
│   │   └── features/
│   │       ├── games/            # MatchupCard, GameStatus, modals
│   │       └── selectors/        # LeagueWeekSelector
│   ├── hooks/                    # useAuth, useContest, useMatchups, useStandings, useTeamCard
│   ├── services/api/             # Axios client + endpoint modules (client, picks, matchups, standings, contestOverview, teamCard, leagues)
│   ├── stores/                   # Zustand auth store
│   ├── types/                    # TypeScript interfaces
│   ├── utils/                    # sportLinks (Sport enum → URL segment resolver, route builders)
│   └── lib/                      # Firebase init, React Query client, utilities
├── constants/Colors.ts           # Theme (light/dark with navy/gold brand palette)
└── __tests__/                    # Unit tests
```

## What's Built (Mobile)

### Functional
- **Authentication**: Firebase email/password sign-in, JWT auto-attached to all requests
- **Picks**: View matchups by league+week, submit straight-up picks, track accuracy
- **Matchup Cards**: Team logos, ranks, records, scores, odds, pick buttons, lock logic (5 min before kickoff)
- **Leaderboard**: View standings for selected league, current user highlighted
- **Game Detail**: Box score, quarter breakdown, game info, leaders
- **Team Card**: Team info, season selector, schedule with game links
- **Home Dashboard**: Pick record widget, standings preview
- **Dark/Light Theme**: Full theme support with brand colors
- **Live Scores**: Polling every 30s for in-progress games

### Partial
- **Stats Comparison Modal**: Data fetching wired, modal UI referenced but may be incomplete
- **AI Preview Modal**: Fully implemented — Claude-generated matchup analysis with Overview, Analysis, and Prediction sections. Includes straight-up and ATS picks with predicted scores, Vegas implied score calculations, and historical data priors. Admin approve/reject workflow with rejection feedback loop. Subscription gating stub in place (UI-only, no backend enforcement). Key product differentiator.
- **League Creation**: Multi-sport create flow (`app/create-league.tsx`) supports NCAAFB, NFL, and MLB (MLB admin-gated via `UserDto.isAdmin`, matching web). NFL + MLB render a division-chip picker; NCAA has a rankings segmented control but no conference picker yet (web fetches conferences dynamically — a mobile Conferences API module is deferred). Date-window picker not yet exposed (full-season only). Endpoints: `leaguesApi.createFootballNcaa/Nfl/BaseballMlbLeague`. Join/discover flows still TODO.
- **Sport-aware Routing**: Team and game screens now live under `(details)/sport/[sport]/[league]/` so cross-sport slug collisions (e.g. a future NBA team sharing a football slug) produce distinct routes. `src/utils/sportLinks.ts` mirrors the web resolver. Still TODO: picks tab surfaces the correct sport via `LeagueWeekMatchupsDto.Sport`; legacy default-arg fallbacks in `teamCardApi` and `useTeamCard` remain for callers that haven't migrated.
- **Profile**: Career/season records hardcoded to 0 (awaiting backend data)
- **Firebase Persistence**: AsyncStorage not yet wired; auth state lost on full restart

### Not Started
- **Push Notifications**: No integration (critical for pick deadlines)
- **Confidence Points**: Schema supports it, UI doesn't expose it
- **Social Auth**: Google/Facebook OAuth not wired (email/password only)
- **Offline Support**: No caching strategy
- **Deep Linking**: Not configured

## Analytical Layer — AI Preview Modal

The AI Preview Modal is the platform's key differentiator. It provides Claude-generated matchup analysis that goes beyond basic stats comparison.

### What It Exposes

Each preview contains three sections:
1. **Overview** — High-level matchup narrative with context
2. **Analysis** — Detailed breakdown of key factors, matchup advantages, and historical trends
3. **Prediction** — Straight-up (SU) and against-the-spread (ATS) picks with predicted scores

The prediction model incorporates:
- Current season performance and trends
- Vegas lines and implied scores (calculated from spread + over/under)
- Historical head-to-head data where available

### Backend Pipeline

Previews are generated by `MatchupPreviewProcessor` in Producer, which calls Claude via `GetMatchupPreviewQueryHandler`. An admin approve/reject workflow gates publication — rejected previews include feedback and are regenerated. The `matchupPreviews` API module serves approved previews to both web and mobile.

### UX

- **Web**: `InsightDialog.jsx` — modal triggered from matchup card, renders Overview/Analysis/Prediction with formatted scores and ATS indicators
- **Mobile**: `InsightModal.tsx` — bottom sheet modal with the same three sections, themed for dark/light mode
- **Availability**: MatchupCard shows a preview button only when `hasPreview` is true for the matchup
- **Subscription gating**: UI checks subscription status before showing content. This is a UI-only stub — no backend enforcement yet.

### Implementation Status

Both web and mobile implementations are **functionally complete**. The backend pipeline (generation, admin review, serving) is fully operational. Outstanding work is limited to subscription enforcement on the backend and potential UX polish.

## Feature Parity: Web vs Mobile

| Feature | Web | Mobile | Priority |
|---------|-----|--------|----------|
| **Picks (straight-up)** | Done | Done | - |
| **Matchup display + scores** | Done | Done | - |
| **Leaderboard/standings** | Done | Done | - |
| **Game detail/overview** | Done | Done | - |
| **Team card** | Done | Done | - |
| **League selector + week selector** | Done | Done | - |
| **Firebase email/password auth** | Done | Done | - |
| **Dark/light theme** | Done | Done | - |
| **AI pick preview / ATS analysis** | Done | Done | **Critical** |
| **Live game updates (SignalR)** | Done | Polling only | Medium |
| **Push notifications (pick reminders)** | N/A | Not started | **Critical** |
| **Confidence points** | Done | Not started | High |
| **League create/join/manage** | Done (multi-sport) | Create: multi-sport (NCAA/NFL + MLB admin-gated); join/manage: Not started | High |
| **League discovery** | Done | Not started | Medium |
| **League invitations** | Done | Not started | Medium |
| **Sport-aware routing** | Done (`/sport/{sport}/{league}/...`) | Done (team + game; picks tab wired) | - |
| **Multi-sport home page (countdown)** | Done (PR #272) | Done (countdown + new-user slot; dashboard widgets still render below) | - |
| **Message board** | Done | Not started | Low |
| **Google/Facebook OAuth** | Done | Not started | High |
| **Rankings/polls** | Done | Not started | Low |
| **Season overview** | Done | Not started | Low (dev tool) |
| **War Room (analytics)** | Done | Not started | Low |
| **Game map** | Done | Not started | Low |
| **Venues** | Done | Not started | Low |
| **Admin dashboard** | Done | Not started | Skip (web-only) |
| **Home dashboard widgets** | Done | Partial | Medium |
| **User profile/settings** | Done | Stub | Medium |
| **Onboarding flow** | Done | Not started | High |

## What Must Ship by August 2026

The minimum viable mobile app for the 2026 season:

### Must Have
1. **Push notifications** — Pick deadline reminders are the #1 engagement driver. The Notification service exists in the backend architecture but doesn't have push capability wired. Needs: Expo push token registration, backend integration (likely via Firebase Cloud Messaging), notification scheduling for pick deadlines.
2. **Social auth (Google at minimum)** — Nobody creates an email/password account on mobile in 2026. Firebase + Expo handles this well.
3. **League join flow** — Users need to get into a league from mobile. Full league creation can wait (commissioners can use web), but joining and league discovery are essential.
4. **Confidence points UI** — If leagues use confidence scoring, mobile users can't be second-class citizens.
5. **Onboarding** — First-run experience: sign up, join a league, understand how picks work.
6. **AI Preview subscription enforcement** — The UI-side gating is in place but backend enforcement is missing. Before launch, the subscription check needs a server-side gate so previews can't be accessed by bypassing the client.

### Should Have
6. **SignalR for live updates** — Replace polling with the same real-time pipeline the web uses.
7. **Firebase persistence** — Wire AsyncStorage so auth survives app restart.
8. **User profile** — Display real career/season records instead of hardcoded zeros.
9. **Home dashboard** — More widgets (leaderboard, upcoming games).

### Can Wait
10. Message board, rankings, venues, analytics, game map — these are engagement features that matter more in Year 3.
11. Admin dashboard — stays web-only permanently.

## Sport-Agnostic Architecture

The mobile app follows the same sport-agnostic contract the web app established in PRs #271 and #272. Three rules to preserve:

1. **Backend returns PascalCase Sport enum names** (`FootballNcaa`, `FootballNfl`, `BaseballMlb`). The `sport` field on `LeagueWeekMatchupsDto` is authoritative; clients don't guess.
2. **URL segments are lowercase tuples** (`/sport/football/ncaa/...`, `/sport/baseball/mlb/...`). Conversion is centralized in `src/utils/sportLinks.ts`:
   - `resolveSportLeague(sportEnum)` returns `{ sport, league }` or `null` for unknown/missing enums (callers must handle null — never silently default to football/ncaa).
   - `teamRoute()` and `gameRoute()` build ready-to-push pathname+params objects so call sites don't hand-write route strings.
3. **API modules accept sport + league explicitly.** `teamCardApi.getBySlugAndSeason(slug, year, sport, league)` and `useTeamCard(slug, year, sport, league)` both take the tuple. Default args remain `football/ncaa` for legacy callers during migration but new callers should pass the resolved values.

Cross-sport slug collisions (a Boise State NCAAFB team vs. a hypothetical future sport with the same slug) are the primary motivation. Without sport in the URL, `/team/boise-state-broncos` is ambiguous. With it, routing is deterministic and the team-card API hits the right sport's database.

When adding a new feature: if it navigates, fetches by slug, or surfaces team/game identity, it needs sport + league in the request. Prefer passing the tuple through props over re-resolving at each component.

## Home Page Design Parity

Web's post-login landing (PR #272, [docs/post-login-landing-design.md](../post-login-landing-design.md)) replaced a stale widget-soup home with a three-tier scaffold:
- Tier 1 (primary slot) — the single "next action" for this user (dual-sport countdown to NCAAFB / NFL kickoff when both are off-season, new-user CTA when no leagues, etc.).
- Tier 2 (context) — sport-specific context cards.
- Tier 3 (secondary) — compact adjacent surfaces.

Mobile's current home (`app/(tabs)/index.tsx`) still renders pick record + standings preview — both of which are empty/stale during off-season. Pending work: port the Tier 1 primary slot (countdown + new-user CTA) to mobile and keep pick record / standings as Tier 2-equivalent content below. No need to mirror web's three-tier CSS exactly; React Native's natural vertical stack is fine as long as the primary slot is unambiguous at the top.

## Code Sharing Between Web and Mobile (future work)

Today `sd-ui` (JS) and `sd-mobile` (TS) are sibling apps with zero shared code. Every DTO, URL builder, API client, and product constant is either duplicated or unilaterally typed on one side. That drift tax is small today but grows with every new sport, endpoint, or DTO field.

**Not shareable** (different render targets):
- JSX / components — React Native `<View>`/`<Text>` vs. DOM `<div>` + CSS.
- Routing — Expo Router vs. React Router.
- Auth transport — Firebase JWT interceptor in mobile, HttpOnly cookie flow on web.

**Shareable (currently duplicated)**:
- **TypeScript types / DTO interfaces** — highest ROI. `models.ts` in mobile is hand-maintained; web is `.js` with no types. Either convert `sd-ui/src/api/*` to TS or generate types from the backend via `openapi-typescript`. The latter also catches backend drift.
- **Pure helpers**: `resolveSportLeague()`, `daysUntil()`, `sportPhrase()`, pick-type unions, `getLeagues(me)`.
- **API URL builders and response parsers** — the `axios.get()` call is per-app (auth differs), but `buildTeamCardUrl(slug, year, sport, league)` and `parseTeamCardResponse(raw)` are not.
- **Zod schemas** for request/response validation.
- **Product copy constants** — kickoff dates, tier copy, CTA labels (so a messaging change lands both places).

**Suggested path**:
1. Convert `sd-ui/src/api/*` to TypeScript (or generate from OpenAPI).
2. Set up pnpm/npm workspaces with `packages/shared-types` + `packages/shared-api-core` (URL builders, parsers, schemas, pure utils).
3. Keep `apiClient.js` / `client.ts` per-app — they inject auth; they're the boundary.
4. Leave React Query hooks per-app for now; once the shared core exists, the hook bodies shrink to one-liners.

Estimated size: 1–2 focused days after web is typed. Defer until after NFL/MLB league creation ships on mobile — the sport-agnostic routing work is already a dry-run of this split. Revisit before the third sport lands; by then duplication cost will outweigh setup cost.

## API Surface

The mobile app consumes the same `/ui/*` endpoints as the web app. No mobile-specific backend work is needed for existing features.

| Module | Endpoints | Status |
|--------|-----------|--------|
| picksApi | getByLeagueAndWeek, submitPick, getWidget | Implemented |
| matchupsApi | getByLeagueAndWeek, getPreview | Implemented |
| standingsApi | getByLeague, getMe | Implemented |
| contestOverviewApi | getOverview | Implemented |
| teamCardApi | getBySlugAndSeason, getStatistics, getMetrics | Implemented |
| leaguesApi | createFootballNcaaLeague, createFootballNflLeague, createBaseballMlbLeague | Create done for all three sports; discover/join/manage TBD |
| messageboardApi | - | Not started |
| rankingsApi | - | Not started |
| seasonApi | - | Not started |
| notificationsApi | - | Not started (backend TBD) |

## Build & Deployment

- **Local dev**: `npx expo start` → Expo Go app on phone via QR code
- **Beta**: `eas build --profile preview` → TestFlight (iOS) / internal (Android)
- **Production**: `eas build --profile production` → App Store / Play Store submission
- **OTA updates**: `eas update` for JS-only changes without store review
- **CI**: GitHub Actions (not Azure Pipelines — EAS is GitHub-native)

See `docs/mobile/expo-deployment-model.md` for detailed deployment workflow.

## Testing

Current coverage is minimal:
- `authStore.test.ts` — Zustand store state mutations
- `leagues.test.ts` — Utility function tests
- `LoadingSpinner.test.tsx` — Component rendering

Testing strategy should prioritize:
1. Hook tests (useContest, useMatchups, usePicks) — these are the data layer
2. Pick submission flow (lock logic, optimistic updates)
3. Auth flow (sign-in, token refresh, sign-out)

## Key Differences from Web App

| Concern | Web (sd-ui) | Mobile (sd-mobile) |
|---------|-------------|-------------------|
| State management | React Context | Zustand + React Query |
| Routing | React Router | Expo Router (file-based) |
| HTTP client | Custom apiClient | Axios with interceptor |
| Real-time | SignalR | Polling (30s) |
| Auth persistence | HttpOnly cookies | In-memory (AsyncStorage TBD) |
| Styling | CSS files | StyleSheet.create() |
| Language | JavaScript | TypeScript |
| Testing | jest + react-scripts | jest-expo + @testing-library/react-native |
