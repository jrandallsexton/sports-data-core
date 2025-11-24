# API Response Wrapper Refactoring

## Overview
Refactor all SportsData.Api endpoints that return naked collections to use wrapper objects. This enables future extensibility for pagination, metadata, and versioning without breaking changes.

## Pattern
```csharp
// ❌ Before (Naked Collection)
public async Task<ActionResult<List<LeagueDto>>> GetLeagues()
{
    return await _service.GetLeaguesAsync();
}

// ✅ After (Wrapped Response)
public async Task<ActionResult<LeaguesResponse>> GetLeagues()
{
    var leagues = await _service.GetLeaguesAsync();
    return new LeaguesResponse { Leagues = leagues };
}
```

## Work Items

### Admin Endpoints (5 items)

#### 1. Wrap `/admin/errors/competitions-without-competitors`
- **File:** `AdminController.cs` (Line 119)
- **Current Return Type:** `List<CompetitionDto>`
- **New Return Type:** `CompetitionErrorsResponse`
- **Wrapper Fields:** `competitions`, `count`, `asOfDate`

#### 2. Wrap `/admin/errors/competitions-without-plays`
- **File:** `AdminController.cs` (Line 125)
- **Current Return Type:** `List<CompetitionDto>`
- **New Return Type:** `CompetitionErrorsResponse`
- **Wrapper Fields:** `competitions`, `count`, `asOfDate`

#### 3. Wrap `/admin/errors/competitions-without-drives`
- **File:** `AdminController.cs` (Line 131)
- **Current Return Type:** `List<CompetitionDto>`
- **New Return Type:** `CompetitionErrorsResponse`
- **Wrapper Fields:** `competitions`, `count`, `asOfDate`

#### 4. Wrap `/admin/errors/competitions-without-metrics`
- **File:** `AdminController.cs` (Line 137)
- **Current Return Type:** `List<CompetitionDto>`
- **New Return Type:** `CompetitionErrorsResponse`
- **Wrapper Fields:** `competitions`, `count`, `asOfDate`

#### 5. Wrap bulk delete operation (POST)
- **File:** `AdminController.cs` (Line 147)
- **Current Input Type:** `List<int>` in request body
- **New Input Type:** `BulkDeleteRequest { competitionIds: int[] }`
- **New Return Type:** `BulkDeleteResponse { deletedCount, failedIds }`

---

### UI Rankings Endpoints (2 items)

#### 6. Wrap `/ui/rankings` (first endpoint)
- **File:** `RankingsController.cs` (Line 25)
- **Current Return Type:** `List<RankingDto>`
- **New Return Type:** `RankingsResponse`
- **Wrapper Fields:** `rankings`, `asOfWeek`, `seasonYear`

#### 7. Wrap `/ui/rankings` (second endpoint)
- **File:** `RankingsController.cs` (Line 56)
- **Current Return Type:** `List<RankingDto>`
- **New Return Type:** `RankingsResponse`
- **Wrapper Fields:** `rankings`, `asOfWeek`, `seasonYear`

---

### UI Picks Endpoints (2 items)

#### 8. Wrap `/ui/picks`
- **File:** `PicksController.cs` (Line 44)
- **Current Return Type:** `List<PickDto>`
- **New Return Type:** `PicksResponse`
- **Wrapper Fields:** `picks`, `totalCount`, `weekNumber`

#### 9. Wrap `/ui/picks/chart`
- **File:** `PicksController.cs` (Line 78)
- **Current Return Type:** `List<PickChartDto>`
- **New Return Type:** `PickChartResponse`
- **Wrapper Fields:** `chartData`, `totalPicks`, `weekRange`

---

### UI Other Endpoints (5 items)

#### 10. Wrap `/ui/analytics`
- **File:** `AnalyticsController.cs` (Line 19)
- **Current Return Type:** `List<AnalyticDto>`
- **New Return Type:** `AnalyticsResponse`
- **Wrapper Fields:** `analytics`, `generatedAt`

#### 11. Wrap `/ui/conferences`
- **File:** `ConferenceController.cs` (Line 17)
- **Current Return Type:** `List<ConferenceDto>`
- **New Return Type:** `ConferencesResponse`
- **Wrapper Fields:** `conferences`, `totalCount`

#### 12. Wrap `/ui/leagues` (first endpoint)
- **File:** `LeagueController.cs` (Line 95)
- **Current Return Type:** `List<LeagueDto>`
- **New Return Type:** `LeaguesResponse`
- **Wrapper Fields:** `leagues`, `totalCount`

#### 13. Wrap `/ui/leagues/discover`
- **File:** `LeagueController.cs` (Line 208)
- **Current Return Type:** `List<LeagueDiscoveryDto>`
- **New Return Type:** `LeagueDiscoveryResponse`
- **Wrapper Fields:** `leagues`, `recommendedCount`, `allCount`

#### 14. Wrap `/ui/leaderboard`
- **File:** `LeaderboardController.cs` (Line 22)
- **Current Return Type:** `List<LeaderboardEntryDto>`
- **New Return Type:** `LeaderboardResponse`
- **Wrapper Fields:** `entries`, `asOfWeek`, `seasonYear`, `totalEntries`

---

## Implementation Steps (Per Endpoint)

1. **Create Response DTO**
   - Add new file in `Application/<Feature>/Responses/`
   - Include collection property + metadata fields
   - Example: `LeaguesResponse.cs`

2. **Update Controller Method**
   - Change return type from `List<T>` to `<Feature>Response`
   - Wrap service result in new response object
   - Add metadata as needed

3. **Update Frontend**
   - Modify API client to access `.leagues` (or equivalent) property
   - Update TypeScript interfaces if using typed clients
   - Test all affected UI components

4. **Update Tests**
   - Modify unit tests to expect wrapped response
   - Update integration tests
   - Verify Swagger/OpenAPI spec updated correctly

5. **Document Breaking Change**
   - Add to CHANGELOG.md
   - Update API documentation
   - Consider API versioning if already in production

---

## Timeline Recommendation

- **Phase 1 (Sprint 1):** Admin endpoints (5 items) - Internal only, low risk
- **Phase 2 (Sprint 2):** UI endpoints (9 items) - Public-facing, coordinate with frontend
- **Phase 3:** Monitor production, gather feedback

---

## Notes

- All admin endpoints can share `CompetitionErrorsResponse` DTO (reusable)
- Consider adding `apiVersion` field to all responses for future-proofing
- Some endpoints may benefit from pagination fields even if not paginated initially
- Coordinate frontend changes with backend deployments

---

## Azure DevOps Work Item Template

```
Title: Wrap [Endpoint] response in parent object
Type: Task
Area Path: API/Refactoring
Iteration: [Current Sprint]

Description:
Refactor [endpoint route] to return wrapped response instead of naked collection.

Current: Returns List<[Type]>
Target: Returns [Type]Response with collection + metadata

Acceptance Criteria:
- [ ] Response DTO created with collection property
- [ ] Controller updated to return wrapped response
- [ ] Frontend updated to consume new structure
- [ ] Unit tests updated
- [ ] Swagger spec reflects new structure
- [ ] No breaking changes for existing clients (if applicable)

Related Work: API Response Wrapper Refactoring Epic
```

---

## Quick Copy-Paste for Azure DevOps

Each item below can be pasted as a new work item:

1. Wrap /admin/errors/competitions-without-competitors - AdminController.cs:119
2. Wrap /admin/errors/competitions-without-plays - AdminController.cs:125
3. Wrap /admin/errors/competitions-without-drives - AdminController.cs:131
4. Wrap /admin/errors/competitions-without-metrics - AdminController.cs:137
5. Wrap admin bulk delete operation - AdminController.cs:147
6. Wrap /ui/rankings (first) - RankingsController.cs:25
7. Wrap /ui/rankings (second) - RankingsController.cs:56
8. Wrap /ui/picks - PicksController.cs:44
9. Wrap /ui/picks/chart - PicksController.cs:78
10. Wrap /ui/analytics - AnalyticsController.cs:19
11. Wrap /ui/conferences - ConferenceController.cs:17
12. Wrap /ui/leagues (first) - LeagueController.cs:95
13. Wrap /ui/leagues/discover - LeagueController.cs:208
14. Wrap /ui/leaderboard - LeaderboardController.cs:22
