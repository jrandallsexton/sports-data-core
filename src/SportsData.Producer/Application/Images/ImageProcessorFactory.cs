using SportsData.Core.Common;
using SportsData.Producer.Application.Images.Processors;
using SportsData.Producer.Infrastructure.Data.Common;

using System.Reflection;
using SportsData.Core.DependencyInjection;

namespace SportsData.Producer.Application.Images
{
    public interface IImageProcessorFactory
    {
        IProcessLogoAndImageRequests GetRequestProcessor(DocumentType documentType);

        IProcessLogoAndImageResponses GetResponseProcessor(DocumentType documentType);
    }

    /// <summary>
    /// Generic image processor factory that works with any sport-specific DbContext.
    /// The generic parameter TDbContext is the concrete context type (FootballDataContext, GolfDataContext, etc.)
    /// which has the MassTransit outbox interceptor registered for transactional event publishing.
    /// </summary>
    public class ImageProcessorFactory<TDbContext> : IImageProcessorFactory
        where TDbContext : BaseDataContext
    {
        private readonly IAppMode _appMode;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IServiceProvider _provider;
        private readonly TDbContext _dataContext;
        private readonly ILogger<ImageProcessorFactory<TDbContext>> _logger;

        private readonly Dictionary<(SourceDataProvider, Sport, DocumentType), Type> _requestProcessors = new();
        private readonly Dictionary<(SourceDataProvider, Sport, DocumentType), Type> _responseProcessors = new();

        public ImageProcessorFactory(
            IAppMode appMode,
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IServiceProvider provider,
            TDbContext dataContext,
            ILogger<ImageProcessorFactory<TDbContext>> logger)
        {
            _appMode = appMode ?? throw new ArgumentNullException(nameof(appMode));
            _documentTypeDecoder = documentTypeDecoder;
            _provider = provider;
            _dataContext = dataContext;
            _logger = logger;

            RegisterProcessors();
        }

        // TODO: refactor this as a singleton or static to avoid re-building the map on every request
        private void RegisterProcessors()
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in types)
            {
                // Request processors
                var requestAttrs = type.GetCustomAttributes<ImageRequestProcessorAttribute>();
                foreach (var attr in requestAttrs)
                {
                    var key = (attr.Source, attr.Sport, attr.DocumentType);

                    if (!_requestProcessors.ContainsKey(key))
                    {
                        _requestProcessors[key] = type;
                        _logger.LogDebug("Registered image request processor: {Processor} for ({Source}, {Sport}, {Type})",
                            type.Name, key.Item1, key.Item2, key.Item3);
                    }
                }

                // Response processors
                var responseAttrs = type.GetCustomAttributes<ImageResponseProcessorAttribute>();
                foreach (var attr in responseAttrs)
                {
                    var key = (attr.Source, attr.Sport, attr.DocumentType);

                    if (!_responseProcessors.ContainsKey(key))
                    {
                        _responseProcessors[key] = type;
                        _logger.LogDebug("Registered image response processor: {Processor} for ({Source}, {Sport}, {Type})",
                            type.Name, key.Item1, key.Item2, key.Item3);
                    }
                }
            }
        }

        public IProcessLogoAndImageRequests GetRequestProcessor(DocumentType documentType)
        {
            var normalizedType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(documentType);
            var key = (SourceDataProvider.Espn, _appMode.CurrentSport, normalizedType);

            if (_requestProcessors.TryGetValue(key, out var openType))
            {
                // CRITICAL: Creates image processors with concrete DbContext for outbox support
                var closedType = openType.IsGenericTypeDefinition
                    ? openType.MakeGenericType(_dataContext.GetType())
                    : openType;

                return (IProcessLogoAndImageRequests)ActivatorUtilities.CreateInstance(_provider, closedType, _dataContext);
            }

            throw new InvalidOperationException($"No image request processor registered for {key}");
        }

        public IProcessLogoAndImageResponses GetResponseProcessor(DocumentType documentType)
        {
            var normalizedType = _documentTypeDecoder.GetLogoDocumentTypeFromDocumentType(documentType);
            var key = (SourceDataProvider.Espn, _appMode.CurrentSport, normalizedType);

            if (_responseProcessors.TryGetValue(key, out var openType))
            {
                // CRITICAL: Creates image processors with concrete DbContext for outbox support
                var closedType = openType.IsGenericTypeDefinition
                    ? openType.MakeGenericType(_dataContext.GetType())
                    : openType;

                return (IProcessLogoAndImageResponses)ActivatorUtilities.CreateInstance(_provider, closedType, _dataContext);
            }

            throw new InvalidOperationException($"No image response processor registered for {key}");
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
        }
    }
}
