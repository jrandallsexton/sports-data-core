using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;

namespace SportsData.Core.Infrastructure.Clients.Contest;

public interface IContestClient
{
    Task<Result<GetSeasonContestsResponse>> GetSeasonContests(
        Guid franchiseId, 
        int seasonYear, 
        int? week = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
    
    Task<Result<GetContestByIdResponse>> GetContestById(
        Guid contestId,
        CancellationToken cancellationToken = default);
}

public class ContestClient(ILogger<ContestClient> logger, HttpClient httpClient) : IContestClient
{
    public async Task<Result<GetSeasonContestsResponse>> GetSeasonContests(
        Guid franchiseId, 
        int seasonYear, 
        int? week = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var weekParam = week.HasValue ? $"&week={week.Value}" : string.Empty;
        var url = $"franchises/{franchiseId}/seasons/{seasonYear}/contests?pageNumber={pageNumber}&pageSize={pageSize}{weekParam}";
        
        using var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return new Failure<GetSeasonContestsResponse>(
                default!,
                MapStatus(response.StatusCode),
                [new FluentValidation.Results.ValidationFailure("StatusCode", $"Failed to get season contests: {response.StatusCode}. Response: {errorContent}")]);
        }

        // Producer endpoint returns JSON array of SeasonContestDto
        List<SeasonContestDto>? contests;
        try
        {
            contests = await response.Content.ReadFromJsonAsync<List<SeasonContestDto>>(cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return new Failure<GetSeasonContestsResponse>(
                default!,
                ResultStatus.BadRequest,
                [new FluentValidation.Results.ValidationFailure("Response", "Unable to deserialize contests response")]);
        }
        catch (NotSupportedException)
        {
            return new Failure<GetSeasonContestsResponse>(
                default!,
                ResultStatus.BadRequest,
                [new FluentValidation.Results.ValidationFailure("Response", "Unable to deserialize contests response")]);
        }
        
        if (contests is null)
        {
            return new Failure<GetSeasonContestsResponse>(
                default!,
                ResultStatus.BadRequest,
                [new FluentValidation.Results.ValidationFailure("Response", "Unable to deserialize contests response")]);
        }
        
        return new Success<GetSeasonContestsResponse>(new GetSeasonContestsResponse(contests));
    }

    public async Task<Result<GetContestByIdResponse>> GetContestById(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var url = $"contests/{contestId}";
        
        using var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Failed to get contest {ContestId}. Status: {StatusCode}. Response: {ErrorContent}", 
                contestId, response.StatusCode, errorContent);
            
            return new Failure<GetContestByIdResponse>(
                default!,
                MapStatus(response.StatusCode),
                [new FluentValidation.Results.ValidationFailure("StatusCode", 
                    "Failed to get contest from external service")]);
        }

        // Deserialize contest from response
        SeasonContestDto? contest;
        try
        {
            contest = await response.Content.ReadFromJsonAsync<SeasonContestDto>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize contest response for {ContestId}", contestId);
            return new Failure<GetContestByIdResponse>(
                default!,
                ResultStatus.BadRequest,
                [new FluentValidation.Results.ValidationFailure("Response", "Unable to deserialize contest response")]);
        }
        
        if (contest is null)
        {
            logger.LogWarning("Contest response deserialized to null for {ContestId}", contestId);
            return new Failure<GetContestByIdResponse>(
                default!,
                ResultStatus.BadRequest,
                [new FluentValidation.Results.ValidationFailure("Response", "Unable to deserialize contest response")]);
        }
        
        return new Success<GetContestByIdResponse>(new GetContestByIdResponse(contest));
    }

    private static ResultStatus MapStatus(HttpStatusCode statusCode) => (int)statusCode switch
    {
        400 => ResultStatus.BadRequest,
        401 => ResultStatus.Unauthorized,
        403 => ResultStatus.Forbid,
        404 => ResultStatus.NotFound,
        >= 500 and < 600 => ResultStatus.Error,
        _ => ResultStatus.Error
    };
}
