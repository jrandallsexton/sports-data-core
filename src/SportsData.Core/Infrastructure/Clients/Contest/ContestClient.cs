using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
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

public class ContestClient(HttpClient httpClient) : IContestClient
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
        
        var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return new Failure<GetSeasonContestsResponse>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("StatusCode", $"Failed to get season contests: {response.StatusCode}. Response: {errorContent}")]);
        }

        var contests = await response.Content.ReadFromJsonAsync<List<SeasonContestDto>>(cancellationToken: cancellationToken);
        
        return new Success<GetSeasonContestsResponse>(new GetSeasonContestsResponse(contests ?? []));
    }

    public async Task<Result<GetContestByIdResponse>> GetContestById(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var url = $"contests/{contestId}";
        
        var response = await httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return new Failure<GetContestByIdResponse>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("StatusCode", $"Failed to get contest: {response.StatusCode}. Response: {errorContent}")]);
        }

        var contest = await response.Content.ReadFromJsonAsync<SeasonContestDto>(cancellationToken: cancellationToken);
        
        return new Success<GetContestByIdResponse>(new GetContestByIdResponse(contest));
    }
}
