using MassTransit;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Infrastructure.Data;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa
{
    public class TeamBySeasonDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<TeamBySeasonDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IBus _bus;

        public TeamBySeasonDocumentProcessor(
            ILogger<TeamBySeasonDocumentProcessor> logger,
            AppDataContext dataContext,
            IBus bus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            // deserialize the DTO
            var espnDto = command.Document.FromJson<EspnTeamSeasonDto>(new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });

            var franchise = await _dataContext.FranchiseExternalIds
                .Include(x => x.Franchise)
                .ThenInclude(x => x.Seasons)
                .FirstOrDefaultAsync(x => x.Value == espnDto.Id.ToString() &&
                                          x.Provider == command.SourceDataProvider);
        }
    }
}
