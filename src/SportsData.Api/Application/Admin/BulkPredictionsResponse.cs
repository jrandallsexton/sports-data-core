namespace SportsData.Api.Application.Admin;

/// <summary>
/// Response DTO for bulk predictions submission
/// </summary>
public class BulkPredictionsResponse
{
    /// <summary>
    /// Number of predictions successfully submitted.
    /// Currently matches TotalCount as the operation is all-or-nothing.
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Total number of predictions in the request
    /// </summary>
    public int TotalCount { get; set; }
    
    /// <summary>
    /// Human-readable message describing the result
    /// </summary>
    public required string Message { get; set; }
}
