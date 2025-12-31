using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Api.Application.UI.Leagues.Commands.AddMatchup;

public interface IAddMatchupCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(AddMatchupCommand command, CancellationToken cancellationToken = default);
}

public class AddMatchupCommandHandler : IAddMatchupCommandHandler
{
    private readonly ILogger<AddMatchupCommandHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IProvideCanonicalData _canonicalDataProvider;
    private readonly IEventBus _eventBus;

    public AddMatchupCommandHandler(
        ILogger<AddMatchupCommandHandler> logger,
        AppDataContext dbContext,
        IProvideCanonicalData canonicalDataProvider,
        IEventBus eventBus)
    {
        _logger = logger;
        _dbContext = dbContext;
        _canonicalDataProvider = canonicalDataProvider;
        _eventBus = eventBus;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        AddMatchupCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "AddMatchupCommandHandler executing. LeagueId={LeagueId}, ContestId={ContestId}, UserId={UserId}",
            command.LeagueId, command.ContestId, command.UserId);

        // 1. Verify league exists and user is commissioner
        var league = await _dbContext.PickemGroups
            .FirstOrDefaultAsync(g => g.Id == command.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.LeagueId), $"League with ID {command.LeagueId} not found.")]);

        if (league.CommissionerUserId != command.UserId)
            return new Failure<Guid>(
                default,
                ResultStatus.Unauthorized,
                [new ValidationFailure(nameof(command.UserId), "Only the league commissioner can add matchups.")]);

        // 2. Check if matchup already exists for this league and contest
        var existingMatchup = await _dbContext.PickemGroupMatchups
            .AnyAsync(m => m.GroupId == command.LeagueId && m.ContestId == command.ContestId, cancellationToken);

        if (existingMatchup)
            return new Failure<Guid>(
                default,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.ContestId), "This matchup already exists in the league.")]);

        // 3. Get matchup data from canonical provider
        var matchup = await _canonicalDataProvider.GetMatchupByContestId(command.ContestId);

        if (matchup is null)
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.ContestId), $"Contest with ID {command.ContestId} not found.")]);

        // 4. Find or create PickemGroupWeek for this league and season week
        var groupWeek = await _dbContext.PickemGroupWeeks
            .FirstOrDefaultAsync(w => w.GroupId == command.LeagueId && w.SeasonWeekId == matchup.SeasonWeekId, cancellationToken);

        if (groupWeek is null)
        {
            groupWeek = new PickemGroupWeek
            {
                Id = Guid.NewGuid(),
                GroupId = command.LeagueId,
                SeasonWeekId = matchup.SeasonWeekId,
                SeasonYear = matchup.SeasonYear,
                SeasonWeek = matchup.SeasonWeek,
                IsNonStandardWeek = false,
                AreMatchupsGenerated = true
            };
            await _dbContext.PickemGroupWeeks.AddAsync(groupWeek, cancellationToken);
            _logger.LogInformation("Created new PickemGroupWeek for LeagueId={LeagueId}, SeasonWeekId={SeasonWeekId}",
                command.LeagueId, matchup.SeasonWeekId);
        }

        // 5. Create the PickemGroupMatchup entity
        var newMatchup = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = command.LeagueId,
            ContestId = command.ContestId,
            SeasonWeekId = matchup.SeasonWeekId,
            SeasonYear = matchup.SeasonYear,
            SeasonWeek = matchup.SeasonWeek,
            Headline = matchup.Headline,
            StartDateUtc = matchup.StartDateUtc,
            Spread = matchup.Spread,
            AwaySpread = matchup.AwaySpread,
            HomeSpread = matchup.HomeSpread,
            AwayRank = matchup.AwayRank,
            HomeRank = matchup.HomeRank,
            AwayWins = matchup.AwayWins,
            AwayLosses = matchup.AwayLosses,
            AwayConferenceWins = matchup.AwayConferenceWins,
            AwayConferenceLosses = matchup.AwayConferenceLosses,
            HomeWins = matchup.HomeWins,
            HomeLosses = matchup.HomeLosses,
            HomeConferenceWins = matchup.HomeConferenceWins,
            HomeConferenceLosses = matchup.HomeConferenceLosses,
            OverUnder = matchup.OverUnder,
            OverOdds = matchup.OverOdds,
            UnderOdds = matchup.UnderOdds,
            CreatedBy = command.UserId,
            CreatedUtc = DateTime.UtcNow,
            GroupWeek = groupWeek
        };

        await _dbContext.PickemGroupMatchups.AddAsync(newMatchup, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Matchup added successfully. LeagueId={LeagueId}, ContestId={ContestId}, MatchupId={MatchupId}",
            command.LeagueId, command.ContestId, newMatchup.Id);

        // 6. Publish event for AI preview generation
        await _eventBus.Publish(
            new PickemGroupMatchupAdded(
                command.LeagueId,
                command.ContestId,
                command.CorrelationId,
                Guid.NewGuid()),
            cancellationToken);

        _logger.LogInformation(
            "Published PickemGroupMatchupAdded event. LeagueId={LeagueId}, ContestId={ContestId}, CorrelationId={CorrelationId}",
            command.LeagueId, command.ContestId, command.CorrelationId);

        return new Success<Guid>(newMatchup.Id);
    }
}
