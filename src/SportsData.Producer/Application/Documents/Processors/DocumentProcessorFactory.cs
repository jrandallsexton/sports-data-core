using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;

using System.Reflection;

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
    private readonly Dictionary<(SourceDataProvider, Sport, DocumentType), Type> _processorMap;

    public DocumentProcessorFactory(
        IServiceProvider serviceProvider,
        ILogger<DocumentProcessorFactory> logger,
        BaseDataContext dataContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _dataContext = dataContext;

        _processorMap = BuildProcessorMap();
    }

    // TODO: refactor this as a singleton or static to avoid re-building the map on every request
    private Dictionary<(SourceDataProvider, Sport, DocumentType), Type> BuildProcessorMap()
    {
        var map = new Dictionary<(SourceDataProvider, Sport, DocumentType), Type>();

        var processorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => typeof(IProcessDocuments).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        foreach (var type in processorTypes)
        {
            var attrs = type.GetCustomAttributes<DocumentProcessorAttribute>();

            foreach (var attr in attrs)
            {
                var key = (attr.Source, attr.Sport, attr.DocumentType);

                if (map.TryGetValue(key, out var value))
                {
                    _logger.LogWarning(
                        "Duplicate DocumentProcessor for ({Source}, {Sport}, {Type}). Using: {Existing}, Ignoring: {Ignored}",
                        key.Source, key.Sport, key.DocumentType, value.Name, type.Name);
                    continue;
                }

                map[key] = type;

                _logger.LogDebug("Registered processor: {Processor} for ({Source}, {Sport}, {Type})",
                    type.Name, key.Source, key.Sport, key.DocumentType);
            }
        }

        return map;
    }

    public IProcessDocuments GetProcessor(SourceDataProvider sourceDataProvider, Sport sport, DocumentType documentType, DocumentAction documentAction)
    {
        var key = (sourceDataProvider, sport, documentType);

        if (!_processorMap.TryGetValue(key, out var openGenericType))
            throw new InvalidOperationException($"No processor registered for ({sourceDataProvider}, {sport}, {documentType})");

        var closedType = openGenericType.IsGenericTypeDefinition ?
            openGenericType.MakeGenericType(_dataContext.GetType()) :
            openGenericType;

        return (IProcessDocuments)ActivatorUtilities.CreateInstance(
            _serviceProvider,
            closedType,
            _dataContext);
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null)!;
        }
    }
}
