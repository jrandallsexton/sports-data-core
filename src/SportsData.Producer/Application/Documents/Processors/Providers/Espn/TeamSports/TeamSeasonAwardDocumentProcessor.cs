using MassTransit;
using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonAward)]
    public class TeamSeasonAwardDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly TDataContext _dataContext;
        private readonly ILogger<TeamSeasonAwardDocumentProcessor<TDataContext>> _logger;
        private readonly IPublishEndpoint _publishEndpoint;

        public TeamSeasonAwardDocumentProcessor(
            TDataContext dataContext,
            ILogger<TeamSeasonAwardDocumentProcessor<TDataContext>> logger,
            IPublishEndpoint publishEndpoint)
        {
            _dataContext = dataContext;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId
            }))
            {
                _logger.LogInformation("Processing TeamSeasonAwardDocument for FranchiseSeason {ParentId}", command.ParentId);
                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            // TODO: Implement deserialization and processing logic for TeamSeasonAward
            _logger.LogError("TODO: Implement TeamSeasonAwardDocumentProcessor.ProcessInternal");
            await Task.Delay(100);
        }
    }
}