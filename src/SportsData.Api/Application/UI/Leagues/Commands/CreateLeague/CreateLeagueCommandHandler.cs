using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Commands.CreateLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Commands.CreateLeague
{
    public interface ICreateLeagueCommandHandler
    {
        Task<Result<Guid>> ExecuteAsync(
            CreateLeagueRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default);
    }

    public class CreateLeagueCommandHandler : ICreateLeagueCommandHandler
    {
        private readonly ILogger<CreateLeagueCommandHandler> _logger;
        private readonly AppDataContext _dbContext;
        private readonly IEventBus _eventBus;
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public CreateLeagueCommandHandler(
            ILogger<CreateLeagueCommandHandler> logger,
            AppDataContext dbContext,
            IEventBus eventBus,
            IProvideCanonicalData canonicalDataProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _eventBus = eventBus;
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task<Result<Guid>> ExecuteAsync(
            CreateLeagueRequest request,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            // === Validation ===
            if (string.IsNullOrWhiteSpace(request.Name))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.Name), "League name is required.")]);

            // === Enum Resolution ===
            if (!Enum.TryParse<PickType>(request.PickType, ignoreCase: true, out var pickType))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.PickType), $"Invalid pick type: {request.PickType}")]);

            if (!Enum.TryParse<TiebreakerType>(request.TiebreakerType, ignoreCase: true, out var tiebreakerType))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.TiebreakerType), $"Invalid tiebreaker type: {request.TiebreakerType}")]);

            if (!Enum.TryParse<TeamRankingFilter>(request.RankingFilter, ignoreCase: true, out var rankingFilter))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.RankingFilter), $"Invalid ranking filter: {request.RankingFilter}")]);

            if (!Enum.TryParse<TiebreakerTiePolicy>(request.TiebreakerTiePolicy, ignoreCase: true, out var tiebreakerTiePolicy))
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.TiebreakerTiePolicy), $"Invalid tiebreaker tie policy: {request.TiebreakerTiePolicy}")]);

            // === Canonical Resolution ===
            var seasonYear = request.SeasonYear ?? DateTime.UtcNow.Year;
            var conferenceIds = request.ConferenceSlugs.Count > 0
                ? await _canonicalDataProvider.GetConferenceIdsBySlugsAsync(
                    Sport.FootballNcaa,
                    seasonYear,
                    request.ConferenceSlugs)
                : new Dictionary<Guid, string>();

            var unresolved = request.ConferenceSlugs
                .Except(conferenceIds.Values, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unresolved.Any())
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(request.ConferenceSlugs), $"Unknown conference slugs: {string.Join(", ", unresolved)}")]);

            // === Create Entity ===
            var group = new PickemGroup
            {
                Id = Guid.NewGuid(),
                CommissionerUserId = currentUserId,
                CreatedBy = currentUserId,
                Description = request.Description?.Trim(),
                IsPublic = request.IsPublic,
                League = League.NCAAF,
                Name = request.Name.Trim(),
                PickType = pickType,
                RankingFilter = rankingFilter,
                Sport = Sport.FootballNcaa,
                TiebreakerTiePolicy = tiebreakerTiePolicy,
                TiebreakerType = tiebreakerType,
                UseConfidencePoints = request.UseConfidencePoints,
                DropLowWeeksCount = request.DropLowWeeksCount
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

            // Add a synthetic to the new group
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
                "Created league {LeagueId} with name {LeagueName} by user {UserId}",
                group.Id,
                group.Name,
                currentUserId);

            // Publish event after successful persistence
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
}
