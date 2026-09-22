# Mobile home: Pick Accuracy card

**Status:** built 2026-09-21, first of the web widgets ported to mobile.
Ships OTA; no native dependency was added.

## What it is

The mobile port of `sd-ui/src/components/widgets/PickAccuracyWidget.jsx`
(the Recharts bar chart reviewed on `/app/admin/widgets`, see
[`admin-widget-gallery.md`](./admin-widget-gallery.md)). One league at a
time: the user's accuracy for each graded week as a bar, the mean of those
weekly percentages as a dashed reference line, and the season figure with
correct/total picks in the header.

Component: `src/UI/sd-mobile/src/components/features/home/PickAccuracyCard.tsx`.

## Where it lives

The Tier 1 slot on Home (`app/(tabs)/index.tsx`), which the off-season
countdown (`PrimarySlotOffSeasonCountdown`) owned outright until now. The
rule, decided in Home rather than inside the card:

| user state | Tier 1 shows |
|---|---|
| no active league | countdown (unchanged) |
| active leagues, none with a graded week yet | countdown (unchanged) |
| at least one active league with a graded week | Pick Accuracy card |

"Active" means present in `/user/me`, which the backend filters to
`DeactivatedUtc IS NULL`. This matters because `GET /ui/picks/chart` has no
season filter: it returns every league the user has ever belonged to, and
`UserPick.Week` is ambiguous across seasons. `activeAccuracyLeagues` in
`src/lib/pickAccuracy.ts` intersects the two and drops ungraded weeks.

Home's spinner gates on both `/user/me` and `/ui/picks/chart` so the slot is
decided once; the card and the countdown never render in sequence on one
load. Both queries are user-scoped and run in parallel. Pull-to-refresh
invalidates the chart key alongside the others.

## Why no charting package

Bars are Views with a height. The mean line is an absolutely positioned
dashed border at `mean% × plot height`. That is the whole chart, and it
means this widget goes out with `eas update` today. A charting package
(react-native-svg or Skia underneath) is a store build and a new runtime
fingerprint; that decision waits for a widget that needs curves or
interaction, and when it comes, batch it with anything else native.

## Deliberate differences from the web widget

- Web colors bars with a green→yellow→red gradient. Mobile uses theme
  tokens: `tint` for weeks at or above the mean, `accentMuted` below it.
  Reads in both themes and says the same thing ("which weeks dragged me
  down") without a traffic-light palette.
- Web uses a `<select>` for leagues. Mobile shows the league name in the
  header when there is one league, and a horizontal chip row when there are
  several. The chips follow `/user/me` order, the same as YourLeaguesCard.
- Web's tooltip (correct/total on hover) has no mobile equivalent; each
  column carries it in its accessibility label instead, and the season
  totals are always visible in the header.
- Web's Y axis is dropped. Every bar is labeled with its percentage, so the
  axis added nothing at phone width.

## Device validation

2026-09-21, operator's admin account, dev client: card renders in the Tier 1
slot with the real chart, colors approved. That account has one league with
graded weeks, so the multi-league chip picker was NOT exercised on a
device; it is covered by the component test and uses the same
ScrollView + TouchableOpacity recipe as the standings week chips. First
production user with two graded leagues is the real test.

## Tests

- `__tests__/lib/pickAccuracy.test.ts`: the active-league intersection, the
  ungraded-week filter, ordering, mean and totals math, formatting.
- `__tests__/components/features/home/PickAccuracyCard.test.tsx`: null on
  empty input, bars per graded week with heights proportional to the
  percentage, mean-line position, single-league header vs multi-league
  chips, chip switching, fallback when the selected league disappears.

## Not done, on purpose

- A league with no graded week is not listed, not even as a chip (owner
  call, 2026-09-21: keep as is). So a user with scored picks in one league
  and none in another sees one league and no picker. The alternative, a
  chip per active league with a "No scored picks yet" state, was offered
  and declined; revisit if users ask where their other league went.

- No tap-through. The card is a status view; "am I winning?" is answered
  here and acted on in the picks and standings tabs.
- No sport filter. A user in an NCAAFB and an NFL league picks between them
  with the chips; the chart is per league, not per sport.
- No handling for a league with more weeks than fit at phone width. NFL's
  18 columns fit at roughly 16px each; a custom-window league is narrower,
  never wider.
