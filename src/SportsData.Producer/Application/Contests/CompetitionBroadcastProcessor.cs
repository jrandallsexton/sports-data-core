using SportsData.Core.Eventing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{

    public class CompetitionBroadcastProcessor
    {
        private readonly ILogger<CompetitionBroadcastProcessor> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IEventBus _bus;

        public CompetitionBroadcastProcessor(
            ILogger<CompetitionBroadcastProcessor> logger,
            TeamSportDataContext dataContext,
            IEventBus bus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
        }

        public async Task Process(CompetitionBroadcastCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Processing CompetitionBroadcastCommand with {@Command}", command);
                try
                {
                    await ProcessInternal(command);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                    throw;
                }
            }
        }

        private async Task ProcessInternal(CompetitionBroadcastCommand command)
        {
            // update status

            // update situation

            // update plays

            // update line score

            // update probabilities

            //TODO: implement the above

            await Task.CompletedTask;
        }
    }

    public class CompetitionBroadcastCommand
    {
        public Guid ContestId { get; set; }

        public Guid CompetitionId { get; }

        public Guid CorrelationId { get; }
    }
}
