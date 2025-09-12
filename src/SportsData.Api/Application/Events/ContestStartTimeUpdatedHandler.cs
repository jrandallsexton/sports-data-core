using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Eventing.Events.Contests;

namespace SportsData.Api.Application.Events
{
    public class ContestStartTimeUpdatedHandler : IConsumer<ContestStartTimeUpdated>
    {
        private readonly ILogger<ContestStartTimeUpdatedHandler> _logger;
        private readonly AppDataContext _dataContext;

        public ContestStartTimeUpdatedHandler(
            ILogger<ContestStartTimeUpdatedHandler> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task Consume(ConsumeContext<ContestStartTimeUpdated> context)
        {
            var msg = context.Message;

            var matchups = await _dataContext.PickemGroupMatchups
                .Where(m => m.ContestId == msg.ContestId)
                .ToListAsync(context.CancellationToken);

            var saveChanges = false;
            foreach (var matchup in matchups)
            {
                if (matchup.StartDateUtc != msg.NewStartTime)
                {
                    _logger.LogInformation("PickemGroupMatchup start time updated. {OldTime}, {NewTime}", matchup.StartDateUtc, msg.NewStartTime);
                    matchup.StartDateUtc = msg.NewStartTime;
                    matchup.ModifiedBy = msg.CorrelationId;
                    matchup.ModifiedUtc = DateTime.UtcNow;
                    saveChanges = true;
                }
            }

            if (saveChanges)
            {
                await _dataContext.SaveChangesAsync(context.CancellationToken);
            }
        }
    }
}
