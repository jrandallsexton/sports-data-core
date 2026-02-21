using MassTransit;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Sourcing.Historical.Saga;
using SportsData.Provider.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SportsData.Provider.Application.Consumers;

/// <summary>
/// Consumes TriggerTierSourcing events from the saga and starts the ResourceIndexJob for that tier.
/// </summary>
public class TriggerTierSourcingConsumer : IConsumer<TriggerTierSourcing>
{
    private readonly ILogger<TriggerTierSourcingConsumer> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IProcessResourceIndexes _resourceIndexJob;

    public TriggerTierSourcingConsumer(
        ILogger<TriggerTierSourcingConsumer> logger,
        AppDataContext dataContext,
        IProcessResourceIndexes resourceIndexJob)
    {
        _logger = logger;
        _dataContext = dataContext;
        _resourceIndexJob = resourceIndexJob;
    }

    public async Task Consume(ConsumeContext<TriggerTierSourcing> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "🎬 TIER_TRIGGERED: Received TriggerTierSourcing. " +
            "CorrelationId={CorrelationId}, Tier={Tier}, TierName={TierName}, Sport={Sport}, Season={Season}",
            message.CorrelationId,
            message.Tier,
            message.TierName,
            message.Sport,
            message.SeasonYear);

        // Find the ResourceIndex job for this tier using CorrelationId as CreatedBy
        var resourceIndex = await _dataContext.ResourceIndexJobs
            .Where(x => x.CreatedBy == message.CorrelationId && 
                       x.DocumentType.ToString() == message.TierName &&
                       x.SeasonYear == message.SeasonYear)
            .FirstOrDefaultAsync();

        if (resourceIndex == null)
        {
            _logger.LogError(
                "❌ TIER_NOT_FOUND: ResourceIndex job not found for tier. " +
                "CorrelationId={CorrelationId}, TierName={TierName}, Season={Season}",
                message.CorrelationId,
                message.TierName,
                message.SeasonYear);
            return;
        }

        var jobDefinition = new DocumentJobDefinition
        {
            ResourceIndexId = resourceIndex.Id,
            Endpoint = resourceIndex.Uri,
            EndpointMask = resourceIndex.EndpointMask,
            Sport = resourceIndex.SportId,
            SourceDataProvider = resourceIndex.Provider,
            DocumentType = resourceIndex.DocumentType,
            SeasonYear = resourceIndex.SeasonYear,
            Shape = resourceIndex.Shape,
            IncludeLinkedDocumentTypes = null,
            StartPage = null
        };

        _logger.LogInformation(
            "▶️ TIER_EXECUTING: Starting ResourceIndexJob. " +
            "ResourceIndexId={ResourceIndexId}, DocumentType={DocumentType}",
            resourceIndex.Id,
            resourceIndex.DocumentType);

        await _resourceIndexJob.ExecuteAsync(jobDefinition);

        _logger.LogInformation(
            "✅ TIER_EXECUTED: ResourceIndexJob completed. " +
            "ResourceIndexId={ResourceIndexId}, DocumentType={DocumentType}",
            resourceIndex.Id,
            resourceIndex.DocumentType);
    }
}
