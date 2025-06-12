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

public class DocumentProcessorFactory : IDocumentProcessorFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentProcessorFactory> _logger;
    private readonly BaseDataContext _dataContext;
    private readonly IDocumentProcessorRegistry _registry;

    public DocumentProcessorFactory(
        IServiceProvider serviceProvider,
        ILogger<DocumentProcessorFactory> logger,
        BaseDataContext dataContext,
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

        var closedType = openGenericType.IsGenericTypeDefinition
            ? openGenericType.MakeGenericType(_dataContext.GetType())
            : openGenericType;

        return (IProcessDocuments)ActivatorUtilities.CreateInstance(
            _serviceProvider,
            closedType,
            _dataContext);
    }
}
