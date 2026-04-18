using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;

public interface ICreateFootballNcaaLeagueCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        CreateFootballNcaaLeagueRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public class CreateFootballNcaaLeagueCommandHandler : ICreateFootballNcaaLeagueCommandHandler
{
    private const Sport SportMode = Sport.FootballNcaa;
    private const League LeagueMode = League.NCAAF;

    private readonly ILogger<CreateFootballNcaaLeagueCommandHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IFranchiseClientFactory _franchiseClientFactory;

    public CreateFootballNcaaLeagueCommandHandler(
        ILogger<CreateFootballNcaaLeagueCommandHandler> logger,
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
        CreateFootballNcaaLeagueRequest request,
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

        TeamRankingFilter? rankingFilter = null;
        if (!string.IsNullOrWhiteSpace(request.RankingFilter))
        {
            if (!Enum.TryParse<TeamRankingFilter>(request.RankingFilter, ignoreCase: true, out var parsed))
                return new Failure<Guid>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.RankingFilter), $"Invalid ranking filter: {request.RankingFilter}")]);
            rankingFilter = parsed;
        }

        var seasonYear = request.SeasonYear ?? DateTime.UtcNow.Year;
        var conferenceIds = request.ConferenceSlugs.Count > 0
            ? await _franchiseClientFactory
                .Resolve(SportMode)
                .GetConferenceIdsBySlugs(seasonYear, request.ConferenceSlugs)
            : new Dictionary<Guid, string>();

        var unresolved = request.ConferenceSlugs
            .Except(conferenceIds.Values, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unresolved.Count > 0)
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(request.ConferenceSlugs), $"Unknown conference slugs: {string.Join(", ", unresolved)}")]);

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
            RankingFilter = rankingFilter,
            Sport = SportMode,
            TiebreakerTiePolicy = tiebreakerTiePolicy,
            TiebreakerType = tiebreakerType,
            UseConfidencePoints = request.UseConfidencePoints,
            DropLowWeeksCount = request.DropLowWeeksCount,
            StartsOn = request.StartsOn,
            EndsOn = request.EndsOn
        };

        foreach (var kvp in conferenceIds)
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
