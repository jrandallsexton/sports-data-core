using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreatePlayerLeague;

public interface ICreatePlayerLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CreatePlayerLeagueCommand request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates a PLAYER Pick'em league. Deliberately standalone from
/// <see cref="CreateLeagueCommandHandlerBase{TRequest}"/>: player leagues
/// carry none of the team-pick configuration (pick type, tiebreakers,
/// confidence, ranking/conference filters), and creation is ADMIN-ONLY
/// while the game is in alpha (week 3-4 go-live) — the standing
/// availability gate models per-sport open dates, not per-game rollout.
///
/// The same PickemGroupCreated outbox event fans out to bootstrap, which
/// creates the league's PickemGroupWeek rows; MatchupScheduleProcessor
/// stamps their phase and SKIPS matchup generation for PlayerPickem
/// groups — the roster is the game, so a team-pick slate would only add
/// dead rows and notification fan-out.
/// </summary>
public class CreatePlayerLeagueCommandHandler : ICreatePlayerLeagueCommandHandler
{
    private const int DefaultMaxUsers = int.MaxValue;

    private readonly ILogger<CreatePlayerLeagueCommandHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IValidator<CreatePlayerLeagueCommand> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreatePlayerLeagueCommandHandler(
        ILogger<CreatePlayerLeagueCommandHandler> logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IContestClientFactory contestClientFactory,
        IValidator<CreatePlayerLeagueCommand> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _contestClientFactory = contestClientFactory;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        CreatePlayerLeagueCommand request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);

        // Alpha gate: Player Pick'em leagues are operator-created until the
        // game launches. Fresh read, never the cached middleware identity —
        // enforcement decisions don't ride caches (same rule as the
        // availability-gate bypass in the team-league base handler).
        var isAdmin = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => u.IsAdmin)
            .FirstOrDefaultAsync(cancellationToken);

        if (!isAdmin)
        {
            _logger.LogWarning(
                "Rejecting create-player-league: user {UserId} is not an admin.",
                currentUserId);
            return new Failure<Guid>(default!, ResultStatus.Forbid,
                [new ValidationFailure(nameof(currentUserId), "Player Pick'em league creation is not open yet.")]);
        }

        var sport = Enum.Parse<Sport>(request.Sport, ignoreCase: true);
        var league = sport == Sport.FootballNfl ? League.NFL : League.NCAAF;

        // Blackout guard, same contract as team leagues: a windowed league
        // whose range holds no games bootstraps to zero weeks. Fail OPEN on
        // dependency errors; reject only a confirmed-empty window.
        if (request.StartsOn.HasValue || request.EndsOn.HasValue)
        {
            var gameDates = await _contestClientFactory
                .Resolve(sport)
                .GetGameDates(request.EffectiveStartsOn, request.EffectiveEndsOn, cancellationToken);

            if (gameDates.IsSuccess && gameDates.Value.Count == 0)
            {
                return new Failure<Guid>(default!, ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.StartsOn),
                        "No games are scheduled in the selected date range. " +
                        "Choose a range that includes at least one game day.")]);
            }
        }

        var joinPolicy = request.JoinPolicy is null
            ? JoinPolicy.Open
            : Enum.Parse<JoinPolicy>(request.JoinPolicy, ignoreCase: true);

        var seasonYear = request.SeasonYear ?? _dateTimeProvider.UtcNow().Year;

        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            CommissionerUserId = currentUserId,
            CreatedBy = currentUserId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Sport = sport,
            League = league,
            GroupType = GroupType.PlayerPickem,
            IsPublic = request.IsPublic,
            JoinPolicy = joinPolicy,
            SeasonYear = seasonYear,
            MaxUsers = DefaultMaxUsers,
            LeagueWindow = request.StartsOn is null && request.EffectiveEndsOn is null
                ? LeagueWindow.FullSeason
                : LeagueWindow.DateRange,
            StartsOn = request.EffectiveStartsOn,
            EndsOn = request.EffectiveEndsOn,
            // Team-pick configuration is inert for this game; set the
            // benign defaults rather than nulls so nothing downstream
            // null-checks a column that was always non-null.
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            UseConfidencePoints = false,
        };

        group.Members.Add(new PickemGroupMember
        {
            CreatedBy = currentUserId,
            PickemGroupId = group.Id,
            Role = LeagueRole.Commissioner,
            UserId = currentUserId,
        });

        await _dbContext.PickemGroups.AddAsync(group, cancellationToken);

        // Outbox: publish BEFORE SaveChanges so the event commits atomically
        // with the aggregate (same contract as the team-league base).
        await _eventBus.Publish(new PickemGroupCreated(
            group.Id,
            group.Name,
            group.CommissionerUserId,
            group.PickType.ToString(),
            null,
            group.Sport,
            seasonYear,
            Guid.NewGuid(),
            Guid.NewGuid()), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // League name deliberately not logged — user-provided values in
        // log entries trip CodeQL (log injection); the id is the join key.
        _logger.LogInformation(
            "Created PlayerPickem league {LeagueId} ({Sport}) by admin {UserId}",
            group.Id, sport, currentUserId);

        return new Success<Guid>(group.Id);
    }
}
