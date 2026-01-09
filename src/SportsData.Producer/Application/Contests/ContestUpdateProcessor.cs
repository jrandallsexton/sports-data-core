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
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId,
                       ["ContestId"] = command.ContestId
                   }))
            {
                _logger.LogInformation(
                    "ContestUpdateProcessor started. ContestId={ContestId}, Provider={Provider}, Sport={Sport}, CorrelationId={CorrelationId}",
                    command.ContestId,
                    command.SourceDataProvider,
                    command.Sport,
                    command.CorrelationId);

                try
                {
                    await ProcessInternal(command);
                    
                    _logger.LogInformation(
                        "ContestUpdateProcessor completed. ContestId={ContestId}, CorrelationId={CorrelationId}",
                        command.ContestId,
                        command.CorrelationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "ContestUpdateProcessor failed. ContestId={ContestId}, CorrelationId={CorrelationId}",
                        command.ContestId,
                        command.CorrelationId);
                    throw;
                }
            }
        }

        private async Task ProcessInternal(UpdateContestCommand command)
        {
            var contest = await _dataContext.Contests
                .Include(c => c.ExternalIds)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == command.ContestId);
            
            if (contest is null)
            {
                _logger.LogError(
                    "Contest not found. ContestId={ContestId}, CorrelationId={CorrelationId}",
                    command.ContestId,
                    command.CorrelationId);
                return;
            }

            var contestExternalId = contest.ExternalIds
                .FirstOrDefault(x => x.Provider == command.SourceDataProvider);

            if (contestExternalId is null)
            {
                _logger.LogError(
                    "Contest external ID not found. ContestId={ContestId}, Provider={Provider}, CorrelationId={CorrelationId}",
                    command.ContestId,
                    command.SourceDataProvider,
                    command.CorrelationId);
                return;
            }

            var contestIdentity = _externalIdentityGenerator.Generate(contestExternalId.SourceUrl);

            _logger.LogInformation(
                "Publishing DocumentRequested for Event. ContestId={ContestId}, Uri={Uri}, CorrelationId={CorrelationId}",
                command.ContestId,
                contestIdentity.CleanUrl,
                command.CorrelationId);

            await _bus.Publish(new DocumentRequested(
                Id: contestIdentity.UrlHash,
                ParentId: null,
                Uri: new Uri(contestIdentity.CleanUrl),
                Ref: null,
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
