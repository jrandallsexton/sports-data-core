# API Response Wrapper Refactoring

## Overview
This document tracks the progress of wrapping API responses in proper DTOs instead of returning "naked" collections or untyped responses.

## Benefits
- Better API documentation (Swagger)
- Improved type safety
- Consistent response structure
- Easier to extend responses with metadata (e.g., pagination, success indicators)

## Completed

### Admin Bulk Predictions Endpoint
- **Endpoint**: `POST /admin/ai-predictions/{syntheticId}`
- **File**: `AdminController.cs:161`
- **Status**: ✅ Completed
- **Changes**:
  - Created `BulkPredictionsResponse` DTO
  - Updated controller to return `ActionResult<BulkPredictionsResponse>`
  - Response now includes:
    - `SuccessCount`: Number of successfully submitted predictions
    - `TotalCount`: Total number of predictions attempted
    - `Message`: Human-readable status message
- **Breaking Change**: Yes - response format changed from empty body to structured response
- **Migration Notes**: 
  - Success responses now return 201 Created with response body
  - Error responses now return 400 Bad Request with response body
  - Frontend clients should update to handle the new response structure

## Pending
- Review other endpoints that return naked collections
- Consider pagination metadata for list endpoints

## Pattern

### Before
```csharp
[HttpPost]
public async Task<IActionResult> SomeEndpoint()
{
    // ... logic
    if (success)
        return Created();
    return BadRequest();
}
```

### After
```csharp
[HttpPost]
public async Task<ActionResult<SomeResponse>> SomeEndpoint()
{
    // ... logic
    if (success)
    {
        var response = new SomeResponse
        {
            // ... populate fields
        };
        return Created($"/path/to/resource", response);
    }
    
    return BadRequest(new SomeResponse
    {
        // ... populate error fields
    });
}
```

## Response DTO Guidelines
1. Use descriptive names ending in `Response` or `Dto`
2. Include success indicators where appropriate
3. Include meaningful messages for both success and error cases
4. Use `required` keyword for mandatory string properties
5. Consider including counts for bulk operations
