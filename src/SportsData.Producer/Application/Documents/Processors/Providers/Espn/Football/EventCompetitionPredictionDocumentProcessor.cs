using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    /// <summary>
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/predictor?lang=en
    /// </summary>
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPrediction)]
    public class EventCompetitionPredictionDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionPredictionDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;

        public EventCompetitionPredictionDocumentProcessor(
            ILogger<EventCompetitionPredictionDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            _logger.LogInformation("Began with {Command}", command);
            // TODO: Implement processing logic
            await Task.Delay(100);
        }
    }
}
