using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Collections.Concurrent;
using System.Text.Json;

namespace SportsData.Provider.Application.Jobs
{
    public interface ISeedResourceIndex
    {
        Task ExecuteAsync(string documentType, string sport, SourceDataProvider provider, Uri rootUrl, int maxDepth);
    }

    [Obsolete("Dynamic crawling is no longer used. See explicit seeding strategy.")]
    public class ResourceIndexSeederJob : ISeedResourceIndex
    {
        private readonly ILogger<ResourceIndexSeederJob> _logger;
        private readonly AppDataContext _db;
        private readonly IProvideEspnApiData _espnApi;
        private readonly IDocumentStore _documentStore;
        private readonly JsonHashCalculator _hashCalculator;
        private readonly IProvideBackgroundJobs _jobQueue;

        private readonly ConcurrentDictionary<string, bool> _visited = new();

        private Guid _resourceIndexId;
        private List<ResourceIndexItem> _items = new();
        private SourceDataProvider _provider;
        private Sport _sport;
        private DocumentType _documentType;

        public ResourceIndexSeederJob(
            ILogger<ResourceIndexSeederJob> logger,
            AppDataContext db,
            IProvideEspnApiData espnApi,
            IDocumentStore documentStore,
            JsonHashCalculator hashCalculator,
            IProvideBackgroundJobs jobQueue)
        {
            _logger = logger;
            _db = db;
            _espnApi = espnApi;
            _documentStore = documentStore;
            _hashCalculator = hashCalculator;
            _jobQueue = jobQueue;
        }

        public async Task ExecuteAsync(
            string documentTypeStr,
            string sportStr,
            SourceDataProvider provider, 
            Uri rootUrl,
            int maxDepth)
        {
            _logger.LogInformation("Starting ResourceIndex seeding from {RootUrl}", rootUrl);

            if (!Enum.TryParse(documentTypeStr, true, out DocumentType documentType))
                throw new ArgumentException("Invalid DocumentType", nameof(documentTypeStr));

            if (!Enum.TryParse(sportStr, true, out Sport sport))
                throw new ArgumentException("Invalid Sport", nameof(sportStr));

            _provider = provider;
            _sport = sport;
            _documentType = documentType;

            // 1. Create a new ResourceIndex
            var resourceIndex = new Infrastructure.Data.Entities.ResourceIndex
            {
                Id = Guid.NewGuid(),
                Provider = _provider,
                SportId = _sport,
                DocumentType = _documentType,
                IsRecurring = false,
                IsEnabled = true,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow,
                Url = rootUrl.ToString(),
                UrlHash = HashProvider.GenerateHashFromUrl(rootUrl.AbsoluteUri),
                Name = Guid.NewGuid().ToString(),
                EndpointMask = null,
                IsSeasonSpecific = false
            };

            _resourceIndexId = resourceIndex.Id;

            // 2. Begin traversal
            //await TraverseAsync(rootUrl, null, 0, maxDepth);
            await TraverseFromFirstInstance(rootUrl, maxDepth);

            var outputToShowBit = _items.Select(x => new { x.Depth, x.Url}).ToJson();

            // 3. Persist to DB
            _logger.LogInformation("Saving ResourceIndex with {ItemCount} items", _items.Count);

            await _db.ResourceIndexJobs.AddAsync(resourceIndex);
            await _db.ResourceIndexItems.AddRangeAsync(_items);
            await _db.SaveChangesAsync();

            // 4. Kick off ResourceIndexJob
            var definition = new DocumentJobDefinition(resourceIndex);
            _jobQueue.Enqueue<IProcessResourceIndexes>(p => p.ExecuteAsync(definition));

            _logger.LogInformation("Seeding complete and ResourceIndexJob enqueued");
        }

        private async Task TraverseFromFirstInstance(Uri listUrl, int maxDepth)
        {
            var listJson = await _espnApi.GetResource(listUrl.ToString(), true);
            var listDoc = JsonDocument.Parse(listJson);

            var firstInstanceRef = ExtractRefs(listDoc).FirstOrDefault();
            if (firstInstanceRef == null)
            {
                _logger.LogWarning("No instance found in resource index {Url}", listUrl);
                return;
            }

            var rootInstanceUrl = new Uri(firstInstanceRef);
            await TraverseOnlyResourceIndexes(rootInstanceUrl, depth: 1, maxDepth);
        }

        private async Task TraverseOnlyResourceIndexes(Uri instanceUrl, int depth, int maxDepth)
        {
            var rawJson = await _espnApi.GetResource(instanceUrl.ToString(), true);
            var doc = JsonDocument.Parse(rawJson);
            var refs = ExtractRefs(doc);

            foreach (var href in refs)
            {
                if (await IsEspnResourceIndex(href))
                {
                    var hash = HashProvider.GenerateHashFromUrl(href);
                    if (_visited.TryAdd(hash, true))
                    {
                        _items.Add(new ResourceIndexItem
                        {
                            Id = Guid.NewGuid(),
                            ResourceIndexId = _resourceIndexId,
                            Url = href,
                            UrlHash = hash,
                            ParentItemId = null,
                            CreatedUtc = DateTime.UtcNow,
                            Depth = depth
                        });

                        if (depth < maxDepth)
                        {
                            await Task.Delay(250);
                            await TraverseOnlyResourceIndexes(new Uri(href), depth + 1, maxDepth);
                        }
                    }
                }
            }
        }



        private async Task TraverseInstanceTree(Uri url, Guid? parentItemId, int depth, int maxDepth)
        {
            if (depth > maxDepth)
            {
                _logger.LogDebug("Max depth {Depth} reached at {Url}", depth, url);
                return;
            }

            var urlHash = HashProvider.GenerateHashFromUrl(url.ToString());
            if (!_visited.TryAdd(urlHash, true))
            {
                _logger.LogDebug("Already visited {Url}", url);
                return;
            }

            try
            {
                var rawJson = await _espnApi.GetResource(url.ToString(), true);

                _items.Add(new ResourceIndexItem
                {
                    Id = Guid.NewGuid(),
                    ResourceIndexId = _resourceIndexId,
                    Url = url.ToString(),
                    UrlHash = urlHash,
                    ParentItemId = parentItemId,
                    CreatedUtc = DateTime.UtcNow,
                    Depth = depth
                });

                var doc = JsonDocument.Parse(rawJson);
                var refs = ExtractRefs(doc);

                foreach (var href in refs)
                {
                    await Task.Delay(250);

                    if (await IsEspnResourceIndex(href))
                    {
                        var indexUrl = new Uri(href);
                        await TraverseInstanceTree(indexUrl, parentItemId: null, depth + 1, maxDepth);
                        //break; // Only recurse into the *first* valid index
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error traversing instance at {Url}", url);
            }
        }


        private async Task<bool> IsEspnResourceIndex(string href)
        {
            try
            {
                var json = await _espnApi.GetResource(href, true);
                var parsed = JsonSerializer.Deserialize<EspnResourceIndexDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return parsed?.Items?.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        //private async Task TraverseAsync(Uri url, Guid? parentItemId = null, int depth = 0, int maxDepth = 5)
        //{
        //    if (depth > maxDepth)
        //    {
        //        _logger.LogDebug("Max depth {MaxDepth} reached at {Url}", maxDepth, url);
        //        return;
        //    }

        //    var urlHash = _hashProvider.GenerateHashFromUrl(url.ToString());

        //    if (!_visited.TryAdd(urlHash, true))
        //    {
        //        _logger.LogDebug("Already visited {Url}", url);
        //        return;
        //    }

        //    try
        //    {
        //        var rawJson = await _espnApi.GetResource(url.ToString(), true);
        //        var hashed = _hashCalculator.NormalizeAndHash(rawJson);

        //        var item = new ResourceIndexItem
        //        {
        //            Id = Guid.NewGuid(),
        //            ResourceIndexId = _resourceIndexId,
        //            Url = url.ToString(),
        //            UrlHash = urlHash,
        //            ParentItemId = parentItemId,
        //            CreatedUtc = DateTime.UtcNow,
        //            Depth = depth
        //        };

        //        _items.Add(item);

        //        var doc = JsonDocument.Parse(rawJson);

        //        var extractedRefs = ExtractRefs(doc).ToList();

        //        if (!extractedRefs.Any())
        //        {
        //            _logger.LogInformation("No references found in {Url}", url);
        //            return;
        //        }

        //        _logger.LogInformation("Extracted {RefCount} references from {Url}", extractedRefs.Count(), url);

        //        foreach (var href in extractedRefs)
        //        {
        //            await Task.Delay(250);
        //            await TraverseAsync(new Uri(href), item.Id, depth + 1, maxDepth);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to fetch or process {Url}", url);
        //    }
        //}


        private IEnumerable<string> ExtractRefs(JsonDocument doc)
        {
            var refs = new List<string>();

            void Extract(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("$ref", out var refProp) && refProp.ValueKind == JsonValueKind.String)
                    {
                        refs.Add(refProp.GetString()!);
                    }

                    foreach (var child in element.EnumerateObject())
                    {
                        Extract(child.Value);
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        Extract(item);
                    }
                }
                // Skip all other types (Number, String, True, False, Null)
            }

            Extract(doc.RootElement);
            return refs.Distinct();
        }

    }
}
