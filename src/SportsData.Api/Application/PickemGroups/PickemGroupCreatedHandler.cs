using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.PickemGroups
{
    public class PickemGroupCreatedHandler : IConsumer<PickemGroupCreated>
    {
        private readonly ILogger<PickemGroupCreatedHandler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PickemGroupCreatedHandler(
            ILogger<PickemGroupCreatedHandler> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalDataProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalDataProvider = canonicalDataProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupCreated> context)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = context.Message.CorrelationId
                   }))
            {
                _logger.LogInformation("New pickem group created event received: {@Message}", context.Message);
                
                await CreateCurrentWeekInternal(context.Message);
            }
        }

        private async Task CreateCurrentWeekInternal(PickemGroupCreated @event)
        {
            var group = await _dataContext.PickemGroups
                .Include(x => x.Weeks)
                .Where(x => x.Id == @event.GroupId)
                .FirstOrDefaultAsync();

            if (group is null)
            {
                _logger.LogError("Group not found");
                throw new Exception("Group not found");
            }

            // get the current week
            var currentWeek = await _canonicalDataProvider.GetCurrentSeasonWeek();

            if (currentWeek is null)
            {
                _logger.LogError("Current week could not be found");
                throw new Exception("Current week could not be found");
            }

            var groupWeek = group.Weeks.FirstOrDefault(x => x.SeasonWeekId == currentWeek.Id);

            if (groupWeek is null)
            {
                groupWeek = new PickemGroupWeek()
                {
                    Id = Guid.NewGuid(),
                    AreMatchupsGenerated = false,
                    GroupId = group.Id,
                    SeasonWeek = currentWeek.WeekNumber,
                    SeasonYear = currentWeek.SeasonYear,
                    SeasonWeekId = currentWeek.Id
                };
                group.Weeks.Add(groupWeek);
                await _dataContext.SaveChangesAsync();
            }

            if (!groupWeek.AreMatchupsGenerated)
            {
                var cmd = new ScheduleGroupWeekMatchupsCommand(
                    group.Id,
                    currentWeek.Id,
                    currentWeek.SeasonYear,
                    currentWeek.WeekNumber,
                    Guid.NewGuid());

                // kick off a process to create the PickemGroupWeek and matchups for the current week
                _backgroundJobProvider.Enqueue<IScheduleGroupWeekMatchups>(p => p.Process(cmd));
            }
        }
    }
}
