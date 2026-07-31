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
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IValidator<TRequest> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILeagueCreationAvailability _availability;

    protected CreateLeagueCommandHandlerBase(
        ILogger logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IFranchiseClientFactory franchiseClientFactory,
        IContestClientFactory contestClientFactory,
        IValidator<TRequest> validator,
        IDateTimeProvider dateTimeProvider,
        ILeagueCreationAvailability availability)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _franchiseClientFactory = franchiseClientFactory;
        _contestClientFactory = contestClientFactory;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
        _availability = availability;
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

        // Availability gate: some sports aren't open for league creation yet — e.g.
        // NCAAFB waits for AP Poll release. This is the correctness floor behind the
        // FE tab-hiding: it closes the deep-link / direct-API path. Reject before any
        // downstream work. See docs/features/league-creation-availability-gate.md.
        var opensUtc = _availability.GetOpensUtc(SportMode);
        if (opensUtc is not null)
        {
            // Admin bypass, mirroring the creation-availability endpoint: the
            // operator needs to exercise gated sports (e.g. NFL) before their
            // season opens. One indexed read, only on the gated path.
            // Deliberately FRESH from the database (not the middleware's
            // 15-minute cached identity the endpoint uses): enforcement
            // decisions never ride a cache; only UI shaping does.
            var isAdmin = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUserId)
                .Select(u => u.IsAdmin)
                .FirstOrDefaultAsync(cancellationToken);

            if (isAdmin)
            {
                _logger.LogInformation(
                    "Creation gate bypassed by admin {UserId}: {Sport} opens {OpensUtc:o}.",
                    currentUserId, SportMode, opensUtc);
            }
            else
            {
                _logger.LogInformation(
                    "Rejecting create-league: {Sport} creation opens {OpensUtc:o}.",
                    SportMode, opensUtc);

                return new Failure<Guid>(
                    default!,
                    ResultStatus.Validation,
                    // User-facing copy — no raw Sport enum; the user is already in a
                    // specific sport's create flow.
                    [new ValidationFailure(nameof(request),
                        $"League creation opens {opensUtc:MMMM d, yyyy}. Check back then.")]);
            }
        }

        // Blackout guard: a windowed league whose date range contains no games
        // bootstraps to zero matchups (e.g. an MLB league created on the
        // All-Star break). Reject up front so we never create an empty league.
        // Full-season leagues (both bounds null) always cover games, so skip the
        // round-trip. See docs/architecture/league-creation-blackout-dates.md.
        if (request.StartsOn.HasValue || request.EndsOn.HasValue)
        {
            var gameDates = await _contestClientFactory
                .Resolve(SportMode)
                .GetGameDates(request.StartsOn, request.EffectiveEndsOn, cancellationToken);

            // Fail OPEN on a client/dependency error: don't block a user-facing
            // create on a transient Producer outage. Only reject on a
            // confirmed-empty window — the actual bug this guards against. The
            // daily MatchupScheduler still backfills if games exist.
            if (gameDates.IsSuccess && gameDates.Value.Count == 0)
            {
                _logger.LogInformation(
                    "Rejecting create-league: no {Sport} games in window {StartsOn:o}..{EndsOn:o}.",
                    SportMode, request.StartsOn, request.EffectiveEndsOn);

                return new Failure<Guid>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.StartsOn),
                        // User-facing copy — no raw Sport enum ("BaseballMlb"); the
                        // user is already in a specific sport's create flow.
                        "No games are scheduled in the selected date range. " +
                        "Choose a range that includes at least one game day.")]);
            }
        }

        // Enum parsing is guaranteed by the validator above.
        var pickType = Enum.Parse<PickType>(request.PickType, ignoreCase: true);
        var tiebreakerType = Enum.Parse<TiebreakerType>(request.TiebreakerType, ignoreCase: true);
        var tiebreakerTiePolicy = Enum.Parse<TiebreakerTiePolicy>(request.TiebreakerTiePolicy, ignoreCase: true);
        // Absent -> Open: pre-existing clients keep today's always-joinable behavior.
        var joinPolicy = request.JoinPolicy is null
            ? JoinPolicy.Open
            : Enum.Parse<JoinPolicy>(request.JoinPolicy, ignoreCase: true);

        // Absent -> infer from the dates. Exact for legacy clients: WeekRange
        // has never been submittable, so dates mean DateRange and their
        // absence means FullSeason.
        var leagueWindow = request.LeagueWindow is not null
            ? Enum.Parse<LeagueWindow>(request.LeagueWindow, ignoreCase: true)
            : (request.StartsOn is null && request.EffectiveEndsOn is null
                ? LeagueWindow.FullSeason
                : LeagueWindow.DateRange);

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
            JoinPolicy = joinPolicy,
            LeagueWindow = leagueWindow,
            League = LeagueMode,
            MaxUsers = DefaultMaxUsers,
            Name = request.Name.Trim(),
            PickType = pickType,
            Sport = SportMode,
            SeasonYear = seasonYear,
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
