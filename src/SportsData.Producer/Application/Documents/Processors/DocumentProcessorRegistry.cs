using SportsData.Core.Common;

using System.Reflection;

namespace SportsData.Producer.Application.Documents.Processors
{
    public interface IDocumentProcessorRegistry
    {
        IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> ProcessorMap { get; }
    }

    public class DocumentProcessorRegistry : IDocumentProcessorRegistry
    {
        public IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> ProcessorMap { get; }

        public DocumentProcessorRegistry(ILogger<DocumentProcessorRegistry> logger)
        {
            ProcessorMap = BuildProcessorMap(logger);
        }

        private static Dictionary<(SourceDataProvider, Sport, DocumentType), Type> BuildProcessorMap(ILogger logger)
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
                        logger.LogWarning(
                            "Duplicate DocumentProcessor for ({Source}, {Sport}, {Type}). Using: {Existing}, Ignoring: {Ignored}",
                            key.Source, key.Sport, key.DocumentType, value.Name, type.Name);
                        continue;
                    }

                    map[key] = type;

                    logger.LogInformation("Registered processor: {Processor} for ({Source}, {Sport}, {Type})",
                        type.Name, key.Source, key.Sport, key.DocumentType);
                }
            }

            return map;
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

}
