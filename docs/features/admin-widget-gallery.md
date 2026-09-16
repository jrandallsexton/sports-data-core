# Admin Widget Gallery — `/app/admin/widgets`

**Status:** plan, 2026-09-15. Nothing built yet.

## Why

`src/UI/sd-ui/src/components/widgets/` holds nine components built for the
2025 season. One of them (`RankingsWidget`) is live on `/app/.../rankings`;
the other eight have **zero importers** — they are not rendered anywhere, so
nobody has looked at them against 2026 data. Meanwhile the home page grew a
parallel set of "cards" (`components/home/RankingsCard`, `AIAccuracyChart`,
`PickAccuracyChart`) that overlap them.

The gallery is a single admin route that stands every widget up with real
data so each one can be reviewed and then promoted, fixed, or deleted. The
first promotion target is AI accuracy for this season.

## Inventory (verified against source, 2026-09-15)

| Widget | Data | Status today | Overlaps |
|---|---|---|---|
| `RankingsWidget` | `/ui/rankings/{season}` (+ `/week/{n}`) | **Live** on the rankings page. Includes `CFPBracket`. | `home/RankingsCard` is a compact Top-10 of the same endpoint. Both stay: page vs. card. |
| `CFPBracket` | `bracket` prop | Only reached via `RankingsWidget` when `pollId === 'cfp'`; CFP is filtered out until November. Uses a mock bracket generator. | — |
| `AiAccuracyWidget` | `syntheticDto` prop ← `/ui/picks/chart/synthetic` | Route exists. Widget never mounted. Recharts bar chart. | `home/AIAccuracyChart.jsx` — no importers; dead twin. |
| `PickAccuracyWidget` | `leagues` prop ← `/ui/picks/chart` | Route exists. Never mounted. | `home/PickAccuracyChart.jsx` — no importers; dead twin. |
| `AiRecordWidget` | `/ui/picks/2025/widget/synthetic` | Route is `{season}/widget/synthetic`; **client hardcodes 2025** (`picksApi.js:7`). Never mounted. | — |
| `PickRecordWidget` | `/ui/picks/2025/widget` | Same **hardcoded 2025** (`picksApi.js:6`). Never mounted. | — |
| `LeaderboardWidget` | `/ui/leaderboard/widget` | Route exists. Never mounted. | Standings pages cover the same data at full size. |
| `NewsWidget` | `/ui/articles`, `/ui/articles/{id}` | Routes exist. Renders a modal via `ReactDOM` portal; imports `home/HomePage.css`. Never mounted. | `home/FeaturedArticleCard`, `home/SystemNews`. |
| `TipWeekWidget` | none — static text, literally titled "(simulated)" | Never mounted. | — |

Two facts drive the design: eight of nine have never rendered against this
season's API, and two of the endpoints are called with a hardcoded season.
Expect breakage on first render — that is the point of the gallery.

## Design

One page, one route, one nav link. No new backend.

**`components/admin/AdminWidgetsPage.jsx`** — same shape as the other admin
pages (`AdminHeader`, `AdminPage.css`, `AdminRoute` guard in `MainApp.jsx`,
a `Link` in `AdminPage.jsx`'s tool nav with the `/app` prefix).

The page renders a list of **frames**. A frame is a card that wraps one
widget with:

- title, source path (`components/widgets/X.jsx`), and the endpoint(s) it hits;
- a status pill the operator sets in the registry (`candidate` / `keep` /
  `delete` / `merge → Y`) — a label, not behavior;
- an **error boundary per frame**, so one widget throwing on 2026 data
  shows a red box with the error and the rest of the page still renders;
- the widget itself, at the width it was designed for (widgets assume the
  home-page grid; the gallery constrains each frame to ~420px and offers a
  "full width" toggle so page-sized ones like `RankingsWidget` are usable).

Props are supplied by the page, mirroring how the home page would mount them:

- `AiAccuracyWidget` ← page fetches `Picks.getAccuracyChartForSynthetic()`.
- `PickAccuracyWidget` ← page fetches `Picks.getAccuracyChartForUser()` (the
  admin's own leagues; fine for review).
- `CFPBracket` ← `RankingsWidget`'s `generateMockBracket` over the current
  AP top 12, so the bracket is reviewable before November.
- Everything else self-fetches.

A small **registry array** at the top of the page is the whole configuration:
`{ key, title, source, endpoints, status, render: () => <Widget .../> }`.
Adding a widget is one entry.

## Fixes that fall out of standing them up

Only what's needed to make the frames render honestly:

1. `picksApi.js` — `getWidgetForUser` / `getWidgetForSynthetic` take a
   `seasonYear` (from `useCurrentSeasonYear`) instead of `2025`. The API
   route already accepts `{season}`.
2. `NewsWidget` — stop importing `home/HomePage.css` from a widget (it drags
   home-page layout rules into the gallery); move the few rules it needs
   into a `NewsWidget.css` like its siblings.
3. Nothing else until a frame is seen against real data.

## Promotion path (the reason for the gallery)

Each frame's status pill records the decision; the keep/delete/merge rule from
`docs/refactor/admin-controller-vsa-split.md` applies here too — **zero
importers is a reason to delete, not to migrate**.

- **AI accuracy** — first up for the season. Decide between
  `AiAccuracyWidget` (bar chart, `chart/synthetic`) and `AiRecordWidget`
  (record + link, `{season}/widget/synthetic`); likely both survive as chart
  + card. Delete the dead `home/AIAccuracyChart.jsx` once decided.
- **Pick accuracy / pick record** — same pair for the user; delete
  `home/PickAccuracyChart.jsx` once decided.
- **Leaderboard** — candidate for the home page next to `YourLeaguesCard`,
  or delete in favor of standings.
- **News** — reconcile with `FeaturedArticleCard` / `SystemNews`; one owner.
- **TipWeek** — delete unless it grows a real data source; it says
  "(simulated)" in its own title.
- **Rankings** — no change; the widget is the page, the card is the teaser.

Promotion itself is per-widget follow-up PRs, not part of this plan.

## Sequence

1. **PR 1 — the gallery.** `AdminWidgetsPage` + route + nav link + frame /
   error boundary + registry with all nine widgets + the two fixes above.
   Web ESLint + Vitest; a smoke test that the page renders every frame title
   and survives one widget throwing.
2. **Review pass** on `/app/admin/widgets` against prod data; set the pills.
3. **Per-widget PRs** in the order above, each deleting its dead twin.

## Open questions

- Should `PickAccuracyWidget` / `PickRecordWidget` in the gallery show the
  admin's own picks (simplest) or take a user picker? Plan assumes own picks.
- `LeaderboardWidget` renders one group; the gallery will show the admin's
  first league. Enough for review?
- Is the `/app/admin/widgets` page itself worth a test of every frame, or
  only the boundary behavior? Plan assumes titles + boundary.
