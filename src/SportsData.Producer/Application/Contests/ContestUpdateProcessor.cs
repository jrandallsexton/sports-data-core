using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Contests
{
    public interface IUpdateContests
    {
        Task Process(UpdateContestCommand command);
    }

    public class ContestUpdateProcessor : IUpdateContests
    {
        private readonly ILogger<ContestUpdateProcessor> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IGenerateExternalRefIdentities _externalIdentityGenerator;

        public ContestUpdateProcessor(
            ILogger<ContestUpdateProcessor> logger,
            FootballDataContext dataContext,
            IEventBus bus,
            IGenerateExternalRefIdentities externalIdentityGenerator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
            _externalIdentityGenerator = externalIdentityGenerator;
        }

        public async Task Process(UpdateContestCommand command)
        {
            var competition = await _dataContext.Competitions
                .Include(c => c.ExternalIds)
                .Include(c => c.Competitors)
                    .ThenInclude(comp => comp.ExternalIds)
                .Include(c => c.Odds)
                .Include(c => c.Contest)
                .Where(c => c.ContestId == command.ContestId)
                .FirstOrDefaultAsync();

            if (competition is null)
            {
                _logger.LogError("Competition could not be loaded for provided contest id. {@Command}", command);
                return;
            }

            var externalId = competition.ExternalIds.FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);
            if (externalId == null)
            {
                _logger.LogError("CompetitionExternalId not found. {@Command}", command);
                return;
            }

            var competitionIdentity = _externalIdentityGenerator.Generate(externalId.SourceUrl);

            await _bus.Publish(new DocumentRequested(
                Id: competitionIdentity.UrlHash,
                ParentId: competition.Id.ToString(),
                Uri: new Uri(competitionIdentity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: command.SeasonYear,
                DocumentType: DocumentType.EventCompetition,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.ContestUpdateProcessor,
                BypassCache: true
            ));
            await _dataContext.OutboxPings.AddAsync(new OutboxPing() { Id = Guid.NewGuid() });
            await _dataContext.SaveChangesAsync();
        }
    }
}
