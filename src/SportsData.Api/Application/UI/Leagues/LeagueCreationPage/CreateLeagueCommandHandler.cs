using SportsData.Api.Application.Events;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Api.Application.UI.Leagues.LeagueCreationPage
{
    public interface ICreateLeagueCommandHandler
    {
        Task<Guid> ExecuteAsync(CreateLeagueCommand command, CancellationToken cancellationToken = default);
    }

    public class CreateLeagueCommandHandler: ICreateLeagueCommandHandler
    {
        private readonly ILogger<CreateLeagueCommandHandler> _logger;
        private readonly AppDataContext _dbContext;
        private readonly IEventBus _eventBus;

        public CreateLeagueCommandHandler(
            ILogger<CreateLeagueCommandHandler> logger,
            AppDataContext dbContext,
            IEventBus eventBus)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dbContext = dbContext;
            _eventBus = eventBus;
        }

        public async Task<Guid> ExecuteAsync(
            CreateLeagueCommand command,
            CancellationToken cancellationToken = default)
        {
            var group = new PickemGroup
            {
                Id = Guid.NewGuid(),
                CommissionerUserId = command.CommissionerUserId,
                CreatedBy = command.CommissionerUserId,
                Description = command.Description,
                IsPublic = command.IsPublic,
                League = command.League,
                Name = command.Name,
                PickType = command.PickType,
                RankingFilter = command.RankingFilter,
                Sport = command.Sport,
                TiebreakerTiePolicy = command.TiebreakerTiePolicy,
                TiebreakerType = command.TiebreakerType,
                UseConfidencePoints = command.UseConfidencePoints,
                DropLowWeeksCount = command.DropLowWeeksCount
            };

            foreach (var kvp in command.Conferences)
            {
                group.Conferences.Add(new PickemGroupConference()
                {
                    ConferenceSlug = kvp.Value,
                    ConferenceId = kvp.Key,
                    PickemGroupId = group.Id
                });
            }

            group.Members.Add(new PickemGroupMember()
            {
                CreatedBy = command.CommissionerUserId,
                PickemGroupId = group.Id,
                Role = LeagueRole.Commissioner,
                UserId = command.CommissionerUserId,
            });

            await _eventBus.Publish(new PickemGroupCreated(
                group.Id,
                Guid.NewGuid(),
                Guid.NewGuid()),
                cancellationToken);

            await _dbContext.PickemGroups.AddAsync(group, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return group.Id;
        }
    }
}