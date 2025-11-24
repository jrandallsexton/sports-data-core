namespace SportsData.Api.Application.Admin;

public class BulkPredictionsResponse
{
    public int SuccessCount { get; set; }
    
    public int TotalCount { get; set; }
    
    public required string Message { get; set; }
}
