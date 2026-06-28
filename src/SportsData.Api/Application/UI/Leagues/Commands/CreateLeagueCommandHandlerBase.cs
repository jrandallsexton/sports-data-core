using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Leagues.Commands;

/// <summary>
/// Shared body for the three sport-specific create-league handlers
/// (NCAA / NFL / MLB). The flow is identical across all three:
/// validate, parse enums, resolve grouping slugs, build the
/// <see cref="PickemGroup"/>, add commissioner + synthetic member,
/// publish <see cref="PickemGroupCreated"/> into the outbox, save.
///
/// <para>
/// Sport-specific bits surface through three abstract members and
/// one virtual hook — see the members below for the exact seam.
/// </para>
///
/// <para>
/// <b>Naming note — grouping vs conference vs division.</b> The
/// <c>PickemGroup.Conferences</c> collection (entity
/// <see cref="PickemGroupConference"/>) is a misnomer: NCAA stores
/// conferences (Big 10, SEC), NFL stores divisions (AFC East, NFC
/// West), MLB stores divisions (AL East, NL Central). The base
/// deliberately uses sport-neutral language ("grouping slugs") and
/// leaves the entity name as-is. A future PR is expected to rename
/// the entity + Franchise client method and decide per-sport which
/// hierarchy level(s) the DTO exposes.
/// </para>
/// </summary>
public abstract class CreateLeagueCommandHandlerBase<TRequest>
    where TRequest : CreateLeagueRequestBase
{
    /// <summary>
    /// Hard-coded placeholder. Per-league user caps are not a current
    /// product feature; the entity column is nullable but we set an
    /// explicit sentinel so downstream code can read the value without
    /// null-checking. Revisit when product decides caps are a thing.
    /// </summary>
    protected const int DefaultMaxUsers = int.MaxValue;

    private readonly ILogger _logger;
    private readonly AppDataContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IValidator<TRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    protected CreateLeagueCommandHandlerBase(
        ILogger logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IFranchiseClientFactory franchiseClientFactory,
        IValidator<TRequest> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _franchiseClientFactory = franchiseClientFactory;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    protected abstract Sport SportMode { get; }
    protected abstract League LeagueMode { get; }

    /// <summary>
    /// The grouping slugs the user picked (conferences for NCAA,
    /// divisions for NFL/MLB). Sport DTOs name the field differently
    /// per sport vocabulary; this hook lets the base stay agnostic.
    /// </summary>
    protected abstract IReadOnlyList<string> GetGroupingSlugs(TRequest request);

    /// <summary>
    /// The request DTO field name that <see cref="GetGroupingSlugs"/>
    /// reads from. Used as the <see cref="ValidationFailure"/> property
    /// name so the FE knows which field to highlight on the
    /// unresolved-slug failure path.
    /// </summary>
    protected abstract string SlugRequestFieldName { get; }

    /// <summary>
    /// Human-friendly singular label for the grouping concept, used in
    /// the unresolved-slug failure message ("Unknown {label} slugs:").
    /// "conference" for NCAA, "division" for NFL/MLB.
    /// </summary>
    protected abstract string SlugDisplayLabel { get; }

    /// <summary>
    /// Hook for fields that exist only on some sports — currently just
    /// NCAA's <see cref="TeamRankingFilter"/>. Called after the base
    /// has filled in all shared fields and before the group is added to
    /// the <see cref="DbContext"/>.
    /// </summary>
    protected virtual void ApplySportSpecific(PickemGroup group, TRequest request) { }

    public async Task<Result<Guid>> ExecuteAsync(
        TRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);

        // Enum parsing is guaranteed by the validator above.
        var pickType = Enum.Parse<PickType>(request.PickType, ignoreCase: true);
        var tiebreakerType = Enum.Parse<TiebreakerType>(request.TiebreakerType, ignoreCase: true);
        var tiebreakerTiePolicy = Enum.Parse<TiebreakerTiePolicy>(request.TiebreakerTiePolicy, ignoreCase: true);

        var seasonYear = request.SeasonYear ?? _dateTimeProvider.UtcNow().Year;
        var slugs = GetGroupingSlugs(request);
        var groupingIds = slugs.Count > 0
            ? await _franchiseClientFactory
                .Resolve(SportMode)
                .GetConferenceIdsBySlugs(seasonYear, slugs.ToList(), cancellationToken)
            : new Dictionary<Guid, string>();

        var unresolved = slugs
            .Except(groupingIds.Values, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unresolved.Count > 0)
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(SlugRequestFieldName, $"Unknown {SlugDisplayLabel} slugs: {string.Join(", ", unresolved)}")]);

        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            CommissionerUserId = currentUserId,
            CreatedBy = currentUserId,
            Description = request.Description?.Trim(),
            IsPublic = request.IsPublic,
            League = LeagueMode,
            MaxUsers = DefaultMaxUsers,
            Name = request.Name.Trim(),
            PickType = pickType,
            Sport = SportMode,
            TiebreakerTiePolicy = tiebreakerTiePolicy,
            TiebreakerType = tiebreakerType,
            UseConfidencePoints = request.UseConfidencePoints,
            DropLowWeeksCount = request.DropLowWeeksCount,
            StartsOn = request.StartsOn,
            EndsOn = request.EffectiveEndsOn
        };

        ApplySportSpecific(group, request);

        foreach (var kvp in groupingIds)
        {
            group.Conferences.Add(new PickemGroupConference
            {
                ConferenceSlug = kvp.Value,
                ConferenceId = kvp.Key,
                PickemGroupId = group.Id
            });
        }

        group.Members.Add(new PickemGroupMember
        {
            CreatedBy = currentUserId,
            PickemGroupId = group.Id,
            Role = LeagueRole.Commissioner,
            UserId = currentUserId,
        });

        var synthetic = await _dbContext.Users
            .Where(x => x.IsSynthetic == true)
            .FirstOrDefaultAsync(cancellationToken);

        if (synthetic != null)
        {
            group.Members.Add(new PickemGroupMember
            {
                CreatedBy = currentUserId,
                PickemGroupId = group.Id,
                Role = LeagueRole.Member,
                UserId = synthetic.Id,
            });
        }

        await _dbContext.PickemGroups.AddAsync(group, cancellationToken);

        // Publish BEFORE the commit: with the EF outbox, Publish enqueues the
        // message into the DbContext tracker and only SaveChangesAsync persists
        // both the aggregate and the outbox row atomically. Publishing AFTER
        // SaveChangesAsync silently loses the event when the DI scope disposes.
        var evt = new PickemGroupCreated(
            group.Id,
            group.Name,
            group.CommissionerUserId,
            group.PickType.ToString(),
            null,
            group.Sport,
            seasonYear,
            Guid.NewGuid(),
            Guid.NewGuid());
        await _eventBus.Publish(evt, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created {Sport} league {LeagueId} with name {LeagueName} by user {UserId}; PickemGroupCreated enqueued to outbox",
            SportMode,
            group.Id,
            group.Name,
            currentUserId);

        return new Success<Guid>(group.Id);
    }
}
