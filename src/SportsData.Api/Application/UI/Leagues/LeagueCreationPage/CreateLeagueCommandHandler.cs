using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Leagues.LeagueCreationPage
{
    public interface ICreateLeagueCommandHandler
    {
        Task<Guid> ExecuteAsync(CreateLeagueCommand command, CancellationToken cancellationToken = default);
    }

    public class CreateLeagueCommandHandler(AppDataContext dbContext) : ICreateLeagueCommandHandler
    {
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
                    ConferenceSlug = kvp.Key,
                    ConferenceId = kvp.Value,
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

            await dbContext.PickemGroups.AddAsync(group, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return group.Id;
        }
    }
}