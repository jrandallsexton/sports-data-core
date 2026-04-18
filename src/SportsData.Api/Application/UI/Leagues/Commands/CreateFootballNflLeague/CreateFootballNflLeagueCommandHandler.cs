using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;

public interface ICreateFootballNflLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CreateFootballNflLeagueRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public class CreateFootballNflLeagueCommandHandler : ICreateFootballNflLeagueCommandHandler
{
    private const Sport SportMode = Sport.FootballNfl;
    private const League LeagueMode = League.NFL;

    private readonly ILogger<CreateFootballNflLeagueCommandHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public CreateFootballNflLeagueCommandHandler(
        ILogger<CreateFootballNflLeagueCommandHandler> logger,
        AppDataContext dbContext,
        IEventBus eventBus,
        IFranchiseClientFactory franchiseClientFactory)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _franchiseClientFactory = franchiseClientFactory;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        CreateFootballNflLeagueRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(request.Name), "League name is required.")]);

        if (!Enum.TryParse<PickType>(request.PickType, ignoreCase: true, out var pickType))
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(request.PickType), $"Invalid pick type: {request.PickType}")]);

        if (!Enum.TryParse<TiebreakerType>(request.TiebreakerType, ignoreCase: true, out var tiebreakerType))
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(request.TiebreakerType), $"Invalid tiebreaker type: {request.TiebreakerType}")]);

        if (!Enum.TryParse<TiebreakerTiePolicy>(request.TiebreakerTiePolicy, ignoreCase: true, out var tiebreakerTiePolicy))
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(request.TiebreakerTiePolicy), $"Invalid tiebreaker tie policy: {request.TiebreakerTiePolicy}")]);

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
            EndsOn = request.EndsOn
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
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created {Sport} league {LeagueId} with name {LeagueName} by user {UserId}",
            SportMode,
            group.Id,
            group.Name,
            currentUserId);

        var evt = new PickemGroupCreated(
            group.Id,
            null,
            group.Sport,
            seasonYear,
            Guid.NewGuid(),
            Guid.NewGuid());

        _logger.LogInformation("Publishing PickemGroupCreated {@Evt}", evt);
        await _eventBus.Publish(evt, cancellationToken);

        return new Success<Guid>(group.Id);
    }
}
