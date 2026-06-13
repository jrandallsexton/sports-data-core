using SportsData.Core.Common;
using SportsData.Producer.Application.Images.Processors;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

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
        // Maps are independent of TDbContext and only depend on attributes scanned across loaded
        // assemblies, so a single AppDomain-wide cache is safe and avoids re-scanning on every
        // scoped resolution.
        private static readonly Lazy<(IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> Requests,
                                      IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> Responses)>
            ProcessorMaps = new(BuildProcessorMaps, LazyThreadSafetyMode.ExecutionAndPublication);

        private readonly IAppMode _appMode;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IServiceProvider _provider;
        private readonly TDbContext _dataContext;
        private readonly ILogger<ImageProcessorFactory<TDbContext>> _logger;

        private IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> _requestProcessors => ProcessorMaps.Value.Requests;
        private IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> _responseProcessors => ProcessorMaps.Value.Responses;

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
        }

        private static (IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> Requests,
                        IReadOnlyDictionary<(SourceDataProvider, Sport, DocumentType), Type> Responses) BuildProcessorMaps()
        {
            var requests = new Dictionary<(SourceDataProvider, Sport, DocumentType), Type>();
            var responses = new Dictionary<(SourceDataProvider, Sport, DocumentType), Type>();

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .Where(t => !t.IsAbstract && !t.IsInterface);

            foreach (var type in types)
            {
                foreach (var attr in type.GetCustomAttributes<ImageRequestProcessorAttribute>())
                {
                    var key = (attr.Source, attr.Sport, attr.DocumentType);
                    requests.TryAdd(key, type);
                }

                foreach (var attr in type.GetCustomAttributes<ImageResponseProcessorAttribute>())
                {
                    var key = (attr.Source, attr.Sport, attr.DocumentType);
                    responses.TryAdd(key, type);
                }
            }

            return (requests, responses);
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
