using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionOdds)]
    public class EventCompetitionOddsDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : FootballDataContext
    {
        private readonly ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public EventCompetitionOddsDocumentProcessor(
            ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Processing EventDocument with {@Command}", command);
                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var externalDto = command.Document.FromJson<EspnEventCompetitionOddsDto>();

            if (externalDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionOddsDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventCompetitionOddsDto Ref is null. {@Command}", command);
                return;
            }

            if (!command.Season.HasValue)
            {
                _logger.LogError("Command must have a SeasonYear defined");
                throw new InvalidOperationException("SeasonYear must be defined in the command.");
            }

            if (!Guid.TryParse(command.ParentId, out var contestId))
            {
                _logger.LogError("ParentId must be a valid Guid for contest ID");
                throw new InvalidOperationException("ParentId must be a valid Guid.");
            }

            var contest = await _dataContext.Contests
                .AsNoTracking()
                .Include(c => c.Odds)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest is null)
            {
                _logger.LogError("Contest not found.  Cannot proceed.");
                throw new InvalidOperationException($"Contest with ID {contestId} not found.");
            }

            var newOdds = externalDto.AsEntity(
                contestId,
                contest.HomeTeamFranchiseSeasonId,
                contest.AwayTeamFranchiseSeasonId);

            var existingOdds = contest.Odds
                .FirstOrDefault(o => o.ProviderId == newOdds.ProviderId);

            if (existingOdds == null)
            {
                _logger.LogInformation("No existing odds found. Adding new odds.");
                await _dataContext.ContestOdds.AddAsync(newOdds);
            }
            else if (existingOdds.HasDifferences(newOdds))
            {
                _logger.LogInformation("Existing odds differ. Updating existing odds.");
                await _dataContext.ContestOdds.AddAsync(newOdds);
            }
            else
            {
                _logger.LogInformation("No changes detected in odds. Skipping update.");
                return;
            }

            await _publishEndpoint.Publish(new ContestOddsCreated(
                contest.Id,
                command.CorrelationId,
                CausationId.Producer.EventDocumentProcessor));

            await _dataContext.SaveChangesAsync();
        }
    }
}
