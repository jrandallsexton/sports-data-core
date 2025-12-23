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
            var contest = await _dataContext.Contests
                .Include(c => c.ExternalIds)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == command.ContestId);
            
            if (contest is null)
            {
                _logger.LogError("Contest not found");
                return;
            }

            var contestExternalId = contest.ExternalIds
                .FirstOrDefault(x => x.Provider == command.SourceDataProvider);

            if (contestExternalId is null)
            {
                _logger.LogError("contestExternalId not found");
                return;
            }

            var contestIdentity = _externalIdentityGenerator.Generate(contestExternalId.SourceUrl);

            await _bus.Publish(new DocumentRequested(
                Id: contestIdentity.UrlHash,
                ParentId: null,
                Uri: new Uri(contestIdentity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: contest.SeasonYear,
                DocumentType: DocumentType.Event,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.ContestUpdateProcessor
            ));

            await _dataContext.SaveChangesAsync();
        }
    }
}
