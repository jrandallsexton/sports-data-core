using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Sql;

using System.Collections.Generic;
using System.Linq;

namespace SportsData.Producer.Application.Franchises.Queries.GetTeamCard;

public interface IGetTeamCardQueryHandler
{
    Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default);
}

public class GetTeamCardQueryHandler : IGetTeamCardQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogoSelectionService _logoSelectionService;

    public GetTeamCardQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider,
        IDateTimeProvider dateTimeProvider,
        ILogoSelectionService logoSelectionService)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
        _dateTimeProvider = dateTimeProvider;
        _logoSelectionService = logoSelectionService;
    }

    public async Task<Result<TeamCardDto>> ExecuteAsync(
        GetTeamCardQuery query,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = _dateTimeProvider.UtcNow();

        // Load franchise season with navigations needed for the team card
        var franchiseSeason = await _dbContext.FranchiseSeasons
            .AsNoTracking()
            .Include(fs => fs.Franchise)
                .ThenInclude(f => f.Logos)
            .Include(fs => fs.Logos)
            .Include(fs => fs.GroupSeason)
            .AsSplitQuery()
            .FirstOrDefaultAsync(fs =>
                fs.Franchise.Slug == query.Slug && fs.SeasonYear == query.SeasonYear,
                cancellationToken);

        if (franchiseSeason is null)
        {
            return new Failure<TeamCardDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("TeamCard", $"Team card not found for slug '{query.Slug}' season {query.SeasonYear}")]);
        }

        var franchise = franchiseSeason.Franchise;

        // Get current ranking: latest week before now, default ranking, ap or cfp
        var latestWeekId = await _dbContext.SeasonWeeks
            .AsNoTracking()
            .Include(sw => sw.Season)
            .Where(sw => sw.Season!.Year == query.SeasonYear && sw.EndDate <= nowUtc)
            .OrderByDescending(sw => sw.EndDate)
            .Select(sw => sw.Id)
            .FirstOrDefaultAsync(cancellationToken);

        int? ranking = null;
        if (latestWeekId != Guid.Empty)
        {
            ranking = await _dbContext.FranchiseSeasonRankings
                .AsNoTracking()
                .Include(r => r.Rank)
                .Where(r =>
                    r.FranchiseSeasonId == franchiseSeason.Id &&
                    r.DefaultRanking &&
                    (r.Type == "ap" || r.Type == "cfp") &&
                    r.SeasonWeekId == latestWeekId)
                .Select(r => (int?)r.Rank.Current)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Select logo through the service with franchise fallback
        var logoUri = _logoSelectionService.SelectWithFallback(
            franchiseSeason.Logos, franchise.Logos);

        // Get venue
        var venue = franchise.VenueId != Guid.Empty
            ? await _dbContext.Venues
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == franchise.VenueId, cancellationToken)
            : null;

        var teamCard = new TeamCardDto
        {
            FranchiseSeasonId = franchiseSeason.Id,
            Slug = franchise.Slug!,
            Name = franchise.DisplayName ?? franchise.Name,
            ShortName = franchise.DisplayNameShort ?? franchise.Name,
            Ranking = ranking,
            ConferenceName = franchiseSeason.GroupSeason?.Name,
            ConferenceShortName = franchiseSeason.GroupSeason?.ShortName,
            ConferenceSlug = franchiseSeason.GroupSeason?.Slug,
            OverallRecord = $"{franchiseSeason.Wins}-{franchiseSeason.Losses}-{franchiseSeason.Ties}",
            ConferenceRecord = $"{franchiseSeason.ConferenceWins}-{franchiseSeason.ConferenceLosses}-{franchiseSeason.ConferenceTies}",
            ColorPrimary = franchise.ColorCodeHex ?? string.Empty,
            ColorSecondary = franchise.ColorCodeAltHex ?? string.Empty,
            LogoUrl = logoUri?.OriginalString ?? string.Empty,
            HelmetUrl = string.Empty,
            Location = franchise.Location ?? string.Empty,
            StadiumName = venue?.Name ?? string.Empty,
            StadiumCapacity = venue?.Capacity ?? 0
        };

        // Keep schedule and seasons as Dapper queries (complex opponent logic)
        var connection = _dbContext.Database.GetDbConnection();
        var parameters = new { query.Slug, query.SeasonYear, NowUtc = nowUtc };

        var seasons = (await connection.QueryAsync<int>(
            new CommandDefinition(_sqlProvider.GetTeamSeasons(), new { query.Slug }, cancellationToken: cancellationToken))).ToList();

        var schedule = (await connection.QueryAsync<TeamCardScheduleItemDto>(
            new CommandDefinition(_sqlProvider.GetTeamCardSchedule(), parameters, cancellationToken: cancellationToken))).ToList();

        var result = teamCard with
        {
            SeasonYears = seasons,
            Schedule = schedule
        };

        return new Success<TeamCardDto>(result);
    }
}
