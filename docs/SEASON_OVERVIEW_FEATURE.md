# Season Overview Feature

## Summary

A new Season Overview page for the web app that allows users to browse college football season data by year, select a week and poll, and view ranked team standings. The page lives at `/app/football/{seasonYear}` — note the intentional omission of `/sport/` from the path, establishing a new shorter route convention that future routes will adopt.

## Motivation

Previously, poll rankings data was available in the system but not surfaced through a dedicated season-level page. Users could view individual contest overviews but had no centralized way to explore season-wide rankings by week and poll. This feature fills that gap.

## Architecture

The feature follows the established API-as-proxy pattern: the UI calls the API, which calls Producer via HTTP. Producer queries the database directly.

### Data Flow

```
UI (/football/2025)
  |
  +--> GET ui/season/2025/overview          (API -> Producer, season metadata)
  |      returns: season name, dates, available weeks, available polls
  |
  +--> GET ui/rankings/2025/week/1/poll/ap  (existing rankings endpoint)
         returns: ranked entries with team logos, records, points, trends
```

The season metadata endpoint is new. The rankings endpoint already existed — this feature reuses it.

### New Endpoint: Season Overview Metadata

**Producer**: `GET api/seasons/{seasonYear}/overview`
**API**: `GET ui/season/{seasonYear}/overview` (authorized)

Returns `SeasonOverviewDto`:
- `SeasonYear`, `Name`, `StartDate`, `EndDate`
- `Weeks[]` — id, number, label, start/end dates
- `Polls[]` — id, name, short name, slug

## Files

### Core (shared library)

| File | Purpose |
|------|---------|
| `Dtos/Canonical/SeasonOverviewDto.cs` | `SeasonOverviewDto`, `SeasonWeekDto`, `SeasonPollSummaryDto` |
| `Infrastructure/Clients/Season/SeasonClient.cs` | Added `GetSeasonOverview` method to existing client |
| `Infrastructure/Clients/Season/SeasonClientFactory.cs` | New factory resolving `SeasonClient` by sport mode |
| `DependencyInjection/ServiceRegistration.cs` | Registered `ISeasonClientFactory` |

### Producer

| File | Purpose |
|------|---------|
| `Application/Seasons/SeasonController.cs` | `GET api/seasons/{seasonYear}/overview` |
| `Application/Seasons/Queries/GetSeasonOverview/GetSeasonOverviewQuery.cs` | Query record |
| `Application/Seasons/Queries/GetSeasonOverview/GetSeasonOverviewQueryHandler.cs` | Queries Season, SeasonWeek, SeasonPoll entities |
| `DependencyInjection/ServiceRegistration.cs` | Registered handler |

### API

| File | Purpose |
|------|---------|
| `Application/UI/Season/SeasonController.cs` | `GET ui/season/{seasonYear}/overview` |
| `Application/UI/Season/Queries/GetSeasonOverview/GetSeasonOverviewQuery.cs` | Query with `SeasonYear` + `Sport` |
| `Application/UI/Season/Queries/GetSeasonOverview/GetSeasonOverviewQueryHandler.cs` | Proxies to Producer via `SeasonClient` |
| `DependencyInjection/ServiceRegistration.cs` | Registered handler |

### UI (sd-ui)

| File | Purpose |
|------|---------|
| `api/seasonApi.js` | API module for season overview |
| `api/apiWrapper.js` | Added `Season` to wrapper |
| `components/season/SeasonOverview.jsx` | Page with year/week/poll selectors and rankings table |
| `components/season/SeasonOverview.css` | Dark-themed styling |
| `MainApp.jsx` | Added route `/football/:seasonYear` |

### Tests

| File | Tests |
|------|-------|
| `Producer.Tests.Unit/.../GetSeasonOverviewQueryHandlerTests.cs` | Season found with weeks/polls; season not found returns 404 |
| `Api.Tests.Unit/.../GetSeasonOverviewQueryHandlerTests.cs` | Success path; failure path |
| `sd-ui/.../SeasonOverview.test.jsx` | Loading state, data rendering, error state, empty state, selector change |

## Configuration

The Season client requires a config key for the Producer URL:

```
CommonConfig:SeasonClientConfig:{Sport}:ApiUrl
```

This follows the same pattern as `ContestClientConfig`, `FranchiseClientConfig`, etc. The value should point to the Producer service base URL (same as the other client configs that target Producer).

## Future Work

- Season leaders by position (deferred from initial scope)
- Remove `/sport/` prefix from existing routes to match this new convention
- Support multiple sports (currently hardcoded to `FootballNcaa`)
