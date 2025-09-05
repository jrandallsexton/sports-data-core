using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Routing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Application.ResourceIndex
{
    [Route("api/resourceIndex")]
    public class ResourceIndexController : ApiControllerBase
    {
        private readonly ILogger<ResourceIndexController> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IGenerateRoutingKeys _routingKeyGenerator = new RoutingKeyGenerator();

        public ResourceIndexController(
            ILogger<ResourceIndexController> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        [HttpPost("create", Name = "CreateResourceIndex")]
        public async Task<IActionResult> CreateResourceIndex([FromBody]CreateResourceIndexCommand command)
        {
            // implement
            var exists = await _dataContext.ResourceIndexJobs
                .Where(x => x.DocumentType == command.DocumentType &&
                            x.SportId == command.Sport &&
                            x.Provider == command.SourceDataProvider &&
                            x.SeasonYear == command.SeasonYear &&
                            x.Uri == command.Ref)
                .FirstOrDefaultAsync();

            var existingCount = await _dataContext.ResourceIndexJobs.CountAsync();

            if (exists is not null)
            {
                return BadRequest("ResourceIndex already exists.");
            }

            var resourceIndexJob = new Infrastructure.Data.Entities.ResourceIndex
            {
                Id = Guid.NewGuid(),
                CronExpression = command.CronExpression,
                DocumentType = command.DocumentType,
                IsEnabled = command.IsEnabled,
                IsRecurring = command.IsRecurring,
                Name = _routingKeyGenerator.Generate(command.SourceDataProvider, command.Ref),
                Provider = command.SourceDataProvider,
                SeasonYear = command.SeasonYear,
                IsSeasonSpecific =  command.SeasonYear.HasValue,
                Shape = command.Shape,
                SourceUrlHash = HashProvider.GenerateHashFromUri(command.Ref),
                SportId = command.Sport,
                Uri = command.Ref,
                Ordinal = command.Ordinal ?? existingCount
            };
            await _dataContext.ResourceIndexJobs.AddAsync(resourceIndexJob);
            await _dataContext.SaveChangesAsync();

            return Ok(resourceIndexJob);
        }
    }

    public record CreateResourceIndexCommand
    {
        public Sport Sport { get; init; }
        public SourceDataProvider SourceDataProvider { get; init; }
        public DocumentType DocumentType { get; init; }
        public int? SeasonYear { get; init; }
        public required Uri Ref { get; init; }
        public bool IsRecurring { get; set; }
        public string? CronExpression { get; set; }
        public bool IsEnabled { get; set; }
        public ResourceShape Shape { get; set; } = ResourceShape.Auto;

        public int? Ordinal { get; set; }
    }

}
