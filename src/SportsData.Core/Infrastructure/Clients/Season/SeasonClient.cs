using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Middleware.Health;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Season;

public interface IProvideSeasons : IProvideHealthChecks
{
    Task<Result<SeasonOverviewDto>> GetSeasonOverview(int seasonYear, CancellationToken ct = default);
    Task<Result<FranchiseSeasonPollDto>> GetPollBySeasonWeekId(Guid seasonWeekId, string pollSlug, CancellationToken ct = default);
}

public class SeasonClient : ClientBase, IProvideSeasons
{
    private readonly ILogger<SeasonClient> _logger;

    public SeasonClient(
        ILogger<SeasonClient> logger,
        HttpClient httpClient) :
        base(httpClient)
    {
        _logger = logger;
    }

    public async Task<Result<SeasonOverviewDto>> GetSeasonOverview(int seasonYear, CancellationToken ct = default)
    {
        if (seasonYear <= 0)
        {
            return new Failure<SeasonOverviewDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("seasonYear", "Season year must be a positive integer")]);
        }

        return await GetAsync<SeasonOverviewDto, SeasonOverviewDto>(
            $"seasons/{seasonYear}/overview",
            overview => overview,
            default!,
            "Season overview",
            ResultStatus.NotFound,
            ct);
    }

    public async Task<Result<FranchiseSeasonPollDto>> GetPollBySeasonWeekId(
        Guid seasonWeekId,
        string pollSlug,
        CancellationToken ct = default)
    {
        return await GetAsync<FranchiseSeasonPollDto, FranchiseSeasonPollDto>(
            $"franchise-season-rankings/by-week/{seasonWeekId}/poll/{pollSlug}",
            poll => poll,
            default!,
            "Rankings",
            ResultStatus.NotFound,
            ct);
    }
}
