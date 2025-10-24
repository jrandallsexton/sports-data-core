using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Competitions
{
    public interface ICompetitionService
    {
        Task<Result<Guid>> RefreshCompetitionDrives(Guid competitionId);
    }

    public class CompetitionService : ICompetitionService
    {
        private readonly TeamSportDataContext _dataContext;
        private readonly IEventBus _eventBus;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public CompetitionService(
            TeamSportDataContext dbContext,
            IEventBus eventBus,
            IGenerateExternalRefIdentities externalRefIdentityGenerator)
        {
            _dataContext = dbContext;
            _eventBus = eventBus;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
        }

        public async Task<Result<Guid>> RefreshCompetitionDrives(Guid competitionId)
        {
            var competition = await _dataContext.Competitions
                .Include(x => x.ExternalIds.Where(y => y.CompetitionId == competitionId))
                .FirstOrDefaultAsync(c => c.Id == competitionId);

            if (competition is null)
            {
                return new Failure<Guid>(
                    competitionId,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(competitionId), "Competition Not Found")]
                );
            }

            var competitionExternalId = competition.ExternalIds
                .FirstOrDefault();

            if (competitionExternalId is null)
            {
                return new Failure<Guid>(
                    competitionId,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(competitionId), "Competition ExternalId Not Found")]
                );
            }

            var sourceUrl = new Uri(competitionExternalId.SourceUrl);

            var drivesRef = EspnUriMapper.CompetitionRefToCompetitionDrivesRef(sourceUrl);
            var drivesIdentity = _externalRefIdentityGenerator.Generate(drivesRef);

            // request sourcing?
            await _eventBus.Publish(new DocumentRequested(
                Id: drivesIdentity.UrlHash,
                ParentId: competitionId.ToString(),
                Uri: new Uri(drivesIdentity.CleanUrl),
                Sport: Sport.FootballNcaa,
                SeasonYear: 2025,
                DocumentType: DocumentType.EventCompetitionDrive,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: Guid.NewGuid(),
                CausationId: CausationId.Producer.CompetitionService
            ));

            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();

            return new Success<Guid>(competitionId, ResultStatus.Accepted);
        }
    }
}
