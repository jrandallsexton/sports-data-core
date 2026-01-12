namespace SportsData.Api.Infrastructure.Refs;

/// <summary>
/// Generates external API resource references for HTTP responses.
/// These refs point to the public-facing API Gateway, not internal services.
/// Used for HATEOAS links in external API responses.
/// </summary>
public interface IGenerateApiResourceRefs
{
    Uri ForVenue(Guid venueId, string sport, string league);
    Uri ForVenues(string sport, string league, int? pageNumber = null, int? pageSize = null);
    Uri ForFranchise(Guid franchiseId, string sport, string league);
    Uri ForFranchises(string sport, string league, int? pageNumber = null, int? pageSize = null);
    Uri ForFranchiseSeasons(Guid franchiseId, string sport, string league);
    Uri ForFranchiseSeason(Guid franchiseId, int year, string sport, string league);
    Uri ForSeasonContests(Guid franchiseId, int year, string sport, string league, int? week = null);
    Uri ForContest(Guid contestId, string sport, string league);
}

public class ApiResourceRefGenerator : IGenerateApiResourceRefs
{
    private readonly string _externalApiBaseUrl;

    public ApiResourceRefGenerator(IConfiguration configuration)
    {
        // External API base URL from Azure AppConfig
        _externalApiBaseUrl = configuration["SportsData.Api:ApiConfig:BaseUrl"]
            ?? throw new InvalidOperationException("SportsData.Api:ApiConfig:BaseUrl not configured in Azure AppConfig");
    }

    public Uri ForVenue(Guid venueId, string sport, string league)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/venues/{venueId}");
    }

    public Uri ForVenues(string sport, string league, int? pageNumber = null, int? pageSize = null)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        
        var queryParams = new List<string>();
        
        if (pageNumber.HasValue)
            queryParams.Add($"pageNumber={pageNumber.Value}");
            
        if (pageSize.HasValue)
            queryParams.Add($"pageSize={pageSize.Value}");
        
        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/venues{query}");
    }

    public Uri ForFranchise(Guid franchiseId, string sport, string league)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/franchises/{franchiseId}");
    }

    public Uri ForFranchises(string sport, string league, int? pageNumber = null, int? pageSize = null)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        
        var queryParams = new List<string>();
        
        if (pageNumber.HasValue)
            queryParams.Add($"pageNumber={pageNumber.Value}");
            
        if (pageSize.HasValue)
            queryParams.Add($"pageSize={pageSize.Value}");
        
        var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
        
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/franchises{query}");
    }

    public Uri ForFranchiseSeasons(Guid franchiseId, string sport, string league)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/franchises/{franchiseId}/seasons");
    }

    public Uri ForFranchiseSeason(Guid franchiseId, int year, string sport, string league)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/franchises/{franchiseId}/seasons/{year}");
    }

    public Uri ForSeasonContests(Guid franchiseId, int year, string sport, string league, int? week = null)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        var weekParam = week.HasValue ? $"?week={week.Value}" : string.Empty;
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/franchises/{franchiseId}/seasons/{year}/contests{weekParam}");
    }

    public Uri ForContest(Guid contestId, string sport, string league)
    {
        var sportLower = sport.ToLowerInvariant();
        var leagueLower = league.ToLowerInvariant();
        return new Uri($"{_externalApiBaseUrl}{sportLower}/{leagueLower}/contests/{contestId}");
    }
}
