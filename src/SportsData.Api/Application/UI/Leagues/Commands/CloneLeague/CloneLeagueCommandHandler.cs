using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Api.Application.UI.Leagues.Commands.CloneLeague;

public interface ICloneLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CloneLeagueCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Duplicates one of the user's leagues into a new one they own: copies the
/// source league's config and re-publishes <see cref="PickemGroupCreated"/> so the
/// bootstrap consumer regenerates the same slate (matchups aren't copied by hand),
/// and — if requested — invites the source league's members. Members and picks are
/// NOT copied; the clone starts fresh (picks are what the import feature is for).
/// A deactivated (wound-down) league can't be cloned.
/// </summary>
public class CloneLeagueCommandHandler : ICloneLeagueCommandHandler
{
    private readonly ILogger<CloneLeagueCommandHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CloneLeagueCommandHandler(
        ILogger<CloneLeagueCommandHandler> logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        CloneLeagueCommand command,
        CancellationToken cancellationToken = default)
    {
        var name = command.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Failure<Guid>(
                default,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Name), "A name is required.")]);
        }

        var source = await _dbContext.PickemGroups
            .AsNoTracking()
            .Include(g => g.Conferences)
            .FirstOrDefaultAsync(g => g.Id == command.SourceLeagueId, cancellationToken);

        if (source is null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.SourceLeagueId), "League not found.")]);
        }

        // Ownership: the caller must belong to the source league.
        var isMember = await _dbContext.PickemGroupMembers
            .AsNoTracking()
            .AnyAsync(m => m.PickemGroupId == source.Id && m.UserId == command.UserId, cancellationToken);

        if (!isMember)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.Forbid,
                [new ValidationFailure(nameof(command.SourceLeagueId), "You can only clone a league you belong to.")]);
        }

        // Honor DeactivatedUtc: a wound-down league (e.g. all contests completed)
        // can't be cloned.
        if (source.DeactivatedUtc is not null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.SourceLeagueId), "This league has ended and can't be cloned.")]);
        }

        // PickemGroup has no SeasonYear column; the slate regenerates from config +
        // season, so derive the season from the source's matchups.
        var seasonYear = await _dbContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == source.Id)
            .Select(m => m.SeasonYear)
            .OrderByDescending(y => y)
            .FirstOrDefaultAsync(cancellationToken);
        if (seasonYear == 0)
            seasonYear = _dateTimeProvider.UtcNow().Year;

        var clone = new PickemGroup
        {
            Id = Guid.NewGuid(),
            CommissionerUserId = command.UserId,
            CreatedBy = command.UserId,
            Name = name,
            Description = source.Description,
            Sport = source.Sport,
            League = source.League,
            PickType = source.PickType,
            TiebreakerType = source.TiebreakerType,
            TiebreakerTiePolicy = source.TiebreakerTiePolicy,
            UseConfidencePoints = source.UseConfidencePoints,
            IsPublic = source.IsPublic,
            MaxUsers = source.MaxUsers,
            DropLowWeeksCount = source.DropLowWeeksCount,
            StartsOn = source.StartsOn,
            EndsOn = source.EndsOn,
            RankingFilter = source.RankingFilter,
            NonStandardWeekGroupSeasonMapFilter = source.NonStandardWeekGroupSeasonMapFilter,
        };

        foreach (var c in source.Conferences)
        {
            clone.Conferences.Add(new PickemGroupConference
            {
                ConferenceSlug = c.ConferenceSlug,
                ConferenceId = c.ConferenceId,
                PickemGroupId = clone.Id,
            });
        }

        clone.Members.Add(new PickemGroupMember
        {
            CreatedBy = command.UserId,
            PickemGroupId = clone.Id,
            Role = LeagueRole.Commissioner,
            UserId = command.UserId,
        });

        var synthetic = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsSynthetic == true)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (synthetic is not null)
        {
            clone.Members.Add(new PickemGroupMember
            {
                CreatedBy = command.UserId,
                PickemGroupId = clone.Id,
                Role = LeagueRole.Member,
                UserId = synthetic.Value,
            });
        }

        await _dbContext.PickemGroups.AddAsync(clone, cancellationToken);

        // Optionally invite the source league's members — same push-deep-link path
        // as invite-by-username. Exclude the cloner and the synthetic user (both
        // already added). These ride the outbox atomically with the writes above.
        if (command.InviteMembers)
        {
            var invitees = await _dbContext.PickemGroupMembers
                .AsNoTracking()
                .Where(m => m.PickemGroupId == source.Id
                            && m.UserId != command.UserId
                            && (synthetic == null || m.UserId != synthetic.Value))
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);

            foreach (var inviteeId in invitees)
            {
                await _eventBus.Publish(
                    new UserInvitedToPickemGroup(
                        InviteeUserId: inviteeId,
                        GroupId: clone.Id,
                        LeagueName: clone.Name,
                        InvitedByUserId: command.UserId,
                        Sport: clone.Sport,
                        SeasonYear: null,
                        CorrelationId: Guid.NewGuid(),
                        CausationId: Guid.NewGuid()),
                    cancellationToken);
            }
        }

        // Regenerate the slate: the bootstrap consumer builds matchups from the
        // clone's config + season, producing the same games as the source.
        // Published BEFORE the commit so it persists atomically via the outbox.
        await _eventBus.Publish(
            new PickemGroupCreated(
                clone.Id,
                clone.Name,
                clone.CommissionerUserId,
                clone.PickType.ToString(),
                null,
                clone.Sport,
                seasonYear,
                Guid.NewGuid(),
                Guid.NewGuid()),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Log every field the bootstrap pipeline actually reads off the clone
        // (Sport, StartsOn, EndsOn, Conferences, RankingFilter) — the event
        // payload's SeasonYear is NOT one of them, since
        // BootstrapLeagueMatchupsProcessor re-reads the group. When a clone
        // bootstraps to an empty slate, this line plus the processor's
        // "resolved N SeasonWeek(s)" / "League window .. filtered X -> Y" tells
        // you which of the copied values was responsible.
        _logger.LogInformation(
            "Cloned league {SourceId} into {CloneId} by user {UserId}; inviteMembers={InviteMembers}, " +
            "seasonYear={SeasonYear}, sport={Sport}, window={StartsOn}..{EndsOn}, " +
            "rankingFilter={RankingFilter}, conferences={ConferenceCount} [{ConferenceSlugs}]",
            source.Id,
            clone.Id,
            command.UserId,
            command.InviteMembers,
            seasonYear,
            clone.Sport,
            clone.StartsOn,
            clone.EndsOn,
            clone.RankingFilter,
            clone.Conferences.Count,
            string.Join(",", clone.Conferences.Select(c => c.ConferenceSlug)));

        return new Success<Guid>(clone.Id);
    }
}
