using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors;

public enum DocumentAction
{
    Created,
    Updated
}

public interface IDocumentProcessorFactory
{
    IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, Sport sport, DocumentType documentType, DocumentAction documentAction);
}

/// <summary>
/// Generic document processor factory that works with any sport-specific DbContext.
/// The generic parameter TDbContext is the concrete context type (FootballDataContext, GolfDataContext, etc.)
/// which has the MassTransit outbox interceptor registered for transactional event publishing.
/// </summary>
public class DocumentProcessorFactory<TDbContext> : IDocumentProcessorFactory
    where TDbContext : BaseDataContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentProcessorFactory<TDbContext>> _logger;
    private readonly TDbContext _dataContext;
    private readonly IDocumentProcessorRegistry _registry;

    public DocumentProcessorFactory(
        IServiceProvider serviceProvider,
        ILogger<DocumentProcessorFactory<TDbContext>> logger,
        TDbContext dataContext,
        IDocumentProcessorRegistry registry)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dataContext = dataContext;
        _registry = registry;
    }

    public IProcessDocuments GetProcessor(
        SourceDataProvider sourceDataProvider,
        Sport sport,
        DocumentType documentType,
        DocumentAction documentAction)
    {
        var key = (sourceDataProvider, sport, documentType);

        if (!_registry.ProcessorMap.TryGetValue(key, out var openGenericType))
            throw new InvalidOperationException($"No processor registered for ({sourceDataProvider}, {sport}, {documentType})");

        // CRITICAL: Creates processors with the concrete DbContext type (FootballDataContext, GolfDataContext, etc.)
        // which has the MassTransit outbox interceptor registered.
        // Example: VenueDocumentProcessor<FootballDataContext> instead of VenueDocumentProcessor<BaseDataContext>
        // This enables transactional outbox for ALL document processors without requiring OutboxPing.
        var closedType = openGenericType.IsGenericTypeDefinition
            ? openGenericType.MakeGenericType(_dataContext.GetType())
            : openGenericType;

        return (IProcessDocuments)ActivatorUtilities.CreateInstance(
            _serviceProvider,
            closedType,
            _dataContext);
    }
}
