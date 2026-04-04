using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Season;

public interface IProvideSeasons : IProvideHealthChecks
{
    Task<Result<SeasonOverviewDto>> GetSeasonOverview(int seasonYear, CancellationToken ct = default);
    Task<Result<FranchiseSeasonPollDto>> GetPollBySeasonWeekId(Guid seasonWeekId, string pollSlug, CancellationToken ct = default);
    Task<Result<CanonicalSeasonWeekDto>> GetCurrentSeasonWeek(CancellationToken ct = default);
    Task<Result<List<CanonicalSeasonWeekDto>>> GetCurrentAndLastSeasonWeeks(CancellationToken ct = default);
    Task<Result<List<CanonicalSeasonWeekDto>>> GetCompletedSeasonWeeks(int seasonYear, CancellationToken ct = default);
}

public class SeasonClient : ClientBase, IProvideSeasons
{
    public SeasonClient(
        ILogger<SeasonClient> logger,
        HttpClient httpClient) :
        base(httpClient)
    {
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
        if (seasonWeekId == Guid.Empty)
        {
            return new Failure<FranchiseSeasonPollDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("seasonWeekId", "Season week ID must not be empty")]);
        }

        if (string.IsNullOrWhiteSpace(pollSlug))
        {
            return new Failure<FranchiseSeasonPollDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("pollSlug", "Poll slug must not be empty")]);
        }

        return await GetAsync<FranchiseSeasonPollDto, FranchiseSeasonPollDto>(
            $"franchise-season-rankings/by-week/{seasonWeekId}/poll/{pollSlug}",
            poll => poll,
            default!,
            "Rankings",
            ResultStatus.NotFound,
            ct);
    }

    public async Task<Result<CanonicalSeasonWeekDto>> GetCurrentSeasonWeek(CancellationToken ct = default)
    {
        return await GetAsync<CanonicalSeasonWeekDto>(
            "seasons/current-week",
            default!,
            "Current season week",
            ResultStatus.NotFound,
            ct);
    }

    public async Task<Result<List<CanonicalSeasonWeekDto>>> GetCurrentAndLastSeasonWeeks(CancellationToken ct = default)
    {
        return await GetAsync<List<CanonicalSeasonWeekDto>>(
            "seasons/current-and-last-weeks",
            new List<CanonicalSeasonWeekDto>(),
            "Current and last season weeks",
            ResultStatus.NotFound,
            ct);
    }

    public async Task<Result<List<CanonicalSeasonWeekDto>>> GetCompletedSeasonWeeks(int seasonYear, CancellationToken ct = default)
    {
        if (seasonYear <= 0)
        {
            return new Failure<List<CanonicalSeasonWeekDto>>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("seasonYear", "Season year must be a positive integer")]);
        }

        return await GetAsync<List<CanonicalSeasonWeekDto>>(
            $"seasons/{seasonYear}/completed-weeks",
            new List<CanonicalSeasonWeekDto>(),
            "Completed season weeks",
            ResultStatus.NotFound,
            ct);
    }
}
