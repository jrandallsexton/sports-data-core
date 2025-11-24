# API Wrapper Refactoring

## Overview
This document tracks the refactoring effort to wrap API responses in response DTO objects instead of returning naked collections.

## Benefits
1. **Extensibility**: Easier to add metadata (pagination, total count, etc.) without breaking changes
2. **Consistency**: Uniform response structure across all endpoints
3. **Type Safety**: Better type inference in frontend code
4. **Future-Proofing**: Allows adding fields without breaking existing clients

## Pattern
Instead of returning:
```csharp
public async Task<ActionResult<List<MyDto>>> GetItems()
```

Use:
```csharp
public async Task<ActionResult<MyResponse>> GetItems()
```

Where the response class follows this structure:
```csharp
public class MyResponse
{
    public List<MyDto> Items { get; set; } = [];
}
```

## Completed Endpoints
- ✅ `/admin/errors/competitions-without-drives` - Returns `CompetitionsWithoutDrivesResponse`

## Pending Endpoints
- `/admin/errors/competitions-without-competitors` - Returns naked `List<CompetitionWithoutCompetitorsDto>`
- `/admin/errors/competitions-without-plays` - Returns naked `List<CompetitionWithoutPlaysDto>`
- `/admin/errors/competitions-without-metrics` - Returns naked `List<CompetitionWithoutMetricsDto>` (not implemented yet)

## Frontend Compatibility
The frontend code in `src/UI/sd-ui/src/components/admin/AdminPage.jsx` already handles both response formats:
```javascript
Array.isArray(res.data) ? res.data : res.data?.items ?? []
```

This allows for gradual migration without breaking changes.
