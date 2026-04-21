using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;

public interface ICreateBaseballMlbLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CreateBaseballMlbLeagueRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public class CreateBaseballMlbLeagueCommandHandler : ICreateBaseballMlbLeagueCommandHandler
{
    private const Sport SportMode = Sport.BaseballMlb;
    private const League LeagueMode = League.MLB;

    private readonly ILogger<CreateBaseballMlbLeagueCommandHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IFranchiseClientFactory _franchiseClientFactory;
    private readonly IValidator<CreateBaseballMlbLeagueRequest> _validator;

    public CreateBaseballMlbLeagueCommandHandler(
        ILogger<CreateBaseballMlbLeagueCommandHandler> logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IFranchiseClientFactory franchiseClientFactory,
        IValidator<CreateBaseballMlbLeagueRequest> validator)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _franchiseClientFactory = franchiseClientFactory;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        CreateBaseballMlbLeagueRequest request,
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

        var seasonYear = request.SeasonYear ?? DateTime.UtcNow.Year;
        var divisionIds = request.DivisionSlugs.Count > 0
            ? await _franchiseClientFactory
                .Resolve(SportMode)
                .GetConferenceIdsBySlugs(seasonYear, request.DivisionSlugs)
            : new Dictionary<Guid, string>();

        var unresolved = request.DivisionSlugs
            .Except(divisionIds.Values, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unresolved.Count > 0)
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(request.DivisionSlugs), $"Unknown division slugs: {string.Join(", ", unresolved)}")]);

        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            CommissionerUserId = currentUserId,
            CreatedBy = currentUserId,
            Description = request.Description?.Trim(),
            IsPublic = request.IsPublic,
            League = LeagueMode,
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

        foreach (var kvp in divisionIds)
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
            null,
            group.Sport,
            seasonYear,
            Guid.NewGuid(),
            Guid.NewGuid());
        await _eventBus.Publish(evt, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created {Sport} league {LeagueId} with name {LeagueName} by user {UserId}; published PickemGroupCreated",
            SportMode,
            group.Id,
            group.Name,
            currentUserId);

        return new Success<Guid>(group.Id);
    }
}
