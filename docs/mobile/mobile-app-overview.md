# Mobile App Overview — SportDeets (sd-mobile)

## Context

The mobile app is the Year 2 priority for the platform. The web app (`sd-ui`) has been the primary UI, but the real engagement surface for a Pick'em product is mobile — nobody opens a browser on Saturday morning to make picks. The goal is to have a functional mobile app in people's pockets before the 2026 NCAAFB season kicks off (first weekend of September).

**Timeline**: Target August 2026 for beta-ready. ~4.5 months from today (March 2026).

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
│   └── (tabs)/
│       ├── index.tsx             # Home (pick record + standings preview)
│       ├── picks.tsx             # Matchup list with league/week selector
│       ├── standings.tsx         # Leaderboard for selected league
│       ├── profile.tsx           # User profile + sign-out
│       └── (details)/
│           ├── game/[id].tsx     # Contest overview
│           └── team/[slug].tsx   # Team card with season selector
├── src/
│   ├── components/
│   │   ├── ui/                   # Button, Card, LoadingSpinner, EmptyState
│   │   └── features/
│   │       ├── games/            # MatchupCard, GameStatus, modals
│   │       └── selectors/        # LeagueWeekSelector
│   ├── hooks/                    # useAuth, useContest, useMatchups, useStandings, useTeamCard
│   ├── services/api/             # Axios client + endpoint modules
│   ├── stores/                   # Zustand auth store
│   ├── types/                    # TypeScript interfaces
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
| **League create/join/manage** | Done | Not started | High |
| **League discovery** | Done | Not started | Medium |
| **League invitations** | Done | Not started | Medium |
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

## API Surface

The mobile app consumes the same `/ui/*` endpoints as the web app. No mobile-specific backend work is needed for existing features.

| Module | Endpoints | Status |
|--------|-----------|--------|
| picksApi | getByLeagueAndWeek, submitPick, getWidget | Implemented |
| matchupsApi | getByLeagueAndWeek, getPreview | Implemented |
| standingsApi | getByLeague, getMe | Implemented |
| contestOverviewApi | getOverview | Implemented |
| teamCardApi | getBySlugAndSeason, getStatistics, getMetrics | Implemented |
| leaguesApi | - | Not started |
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
