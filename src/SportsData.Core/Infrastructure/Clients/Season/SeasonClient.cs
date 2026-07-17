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
    Task<Result<CurrentSeasonDto>> GetCurrentSeason(CancellationToken ct = default);
    Task<Result<FranchiseSeasonPollDto>> GetPollBySeasonWeekId(Guid seasonWeekId, string pollSlug, CancellationToken ct = default);
    Task<Result<CanonicalSeasonWeekDto>> GetCurrentSeasonWeek(CancellationToken ct = default);
    Task<Result<List<CanonicalSeasonWeekDto>>> GetCurrentAndLastSeasonWeeks(CancellationToken ct = default);
    Task<Result<List<CanonicalSeasonWeekDto>>> GetCompletedSeasonWeeks(int seasonYear, CancellationToken ct = default);
    Task<Result<List<CanonicalSeasonWeekDto>>> GetSeasonWeeksOverlapping(DateTime from, DateTime to, CancellationToken ct = default);
    Task<RankingsByPollIdByWeekDto> GetRankingsByPollByWeek(string poll, int seasonYear, int weekNumber, MarkDirection direction, CancellationToken ct = default);
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

    public async Task<Result<CurrentSeasonDto>> GetCurrentSeason(CancellationToken ct = default)
    {
        return await GetAsync<CurrentSeasonDto>(
            "seasons/current",
            default!,
            "Current season",
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

    public async Task<Result<List<CanonicalSeasonWeekDto>>> GetSeasonWeeksOverlapping(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        if (from > to)
        {
            return new Failure<List<CanonicalSeasonWeekDto>>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(from), "from must be on or before to")]);
        }

        // ISO 8601 round-trip ("o") is the canonical wire format ASP.NET's
        // [FromQuery] DateTime model binder reads back without timezone drift.
        return await GetAsync<List<CanonicalSeasonWeekDto>>(
            $"seasons/weeks/by-date-range?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}",
            new List<CanonicalSeasonWeekDto>(),
            "Season weeks by date range",
            ResultStatus.NotFound,
            ct);
    }

    public async Task<RankingsByPollIdByWeekDto> GetRankingsByPollByWeek(string poll, int seasonYear, int weekNumber, MarkDirection direction, CancellationToken ct = default)
    {
        return await GetOrDefaultAsync(
            $"franchise-season-rankings/by-poll?poll={poll}&seasonYear={seasonYear}&weekNumber={weekNumber}&direction={direction}",
            new RankingsByPollIdByWeekDto
            {
                PollName = poll,
                PollId = poll,
                SeasonYear = seasonYear,
                Week = weekNumber
            },
            ct);
    }
}
