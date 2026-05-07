using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    public interface IUpdateContests
    {
        Task Process(UpdateContestCommand command);
    }

    public class ContestUpdateProcessor<TDataContext> : IUpdateContests
        where TDataContext : TeamSportDataContext
    {
        /// <summary>
        /// Document types to source on a "Refresh Contest" request. Anything
        /// outside this set is filtered out at every spawn site downstream
        /// (via DocumentProcessorBase.ShouldSpawn). Excludes roster, per-team
        /// statistics, and athlete-level docs — those are sourced once during
        /// initial ingestion and don't change on a contest refresh.
        ///
        /// See docs/refresh-contest-cascade-narrowing.md for rationale.
        /// </summary>
        private static readonly List<DocumentType> ContestRefreshDocumentTypes = new()
        {
            DocumentType.Event,
            DocumentType.EventCompetition,
            DocumentType.EventCompetitionStatus,
            DocumentType.EventCompetitionSituation,
            DocumentType.EventCompetitionBroadcast,
            DocumentType.EventCompetitionOdds,
            DocumentType.EventCompetitionCompetitor,
            DocumentType.EventCompetitionCompetitorScore,
            DocumentType.EventCompetitionCompetitorLineScore,
            DocumentType.EventCompetitionCompetitorRecord,
            DocumentType.EventCompetitionPlay,
            DocumentType.EventCompetitionDrive,
            DocumentType.EventCompetitionLeaders,
            DocumentType.EventCompetitionProbability
        };

        private readonly ILogger<ContestUpdateProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IGenerateExternalRefIdentities _externalIdentityGenerator;

        public ContestUpdateProcessor(
            ILogger<ContestUpdateProcessor<TDataContext>> logger,
            TDataContext dataContext,
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
                       ["ContestId"] = command.ContestId,
                       ["Provider"] = command.SourceDataProvider,
                       ["Sport"] = command.Sport
                   }))
            {
                _logger.LogInformation("ContestUpdateProcessor started");

                try
                {
                    await ProcessInternal(command);
                    
                    _logger.LogInformation("ContestUpdateProcessor completed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ContestUpdateProcessor failed");
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
                _logger.LogError("Contest not found");
                return;
            }

            var contestExternalId = contest.ExternalIds
                .FirstOrDefault(x => x.Provider == command.SourceDataProvider);

            if (contestExternalId is null)
            {
                _logger.LogError("Contest external ID not found");
                return;
            }

            var contestIdentity = _externalIdentityGenerator.Generate(contestExternalId.SourceUrl);

            _logger.LogInformation(
                "Publishing DocumentRequested for Event. Uri={Uri}",
                contestIdentity.CleanUrl);

            var evt = new DocumentRequested(
                Id: contestIdentity.UrlHash,
                ParentId: null,
                Uri: new Uri(contestIdentity.CleanUrl),
                Ref: null,
                Sport: command.Sport,
                SeasonYear: contest.SeasonYear,
                DocumentType: DocumentType.Event,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.ContestUpdateProcessor,
                IncludeLinkedDocumentTypes: ContestRefreshDocumentTypes
            );

            _logger.LogInformation("Publishing with {@evt}", evt);

            await _bus.Publish(evt);

            await _dataContext.SaveChangesAsync();
        }
    }
}
