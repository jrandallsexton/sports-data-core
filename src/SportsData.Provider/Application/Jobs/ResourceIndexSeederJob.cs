
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;

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
            IProvideBackgroundJobs jobQueue)
        {
            _logger = logger;
            _db = db;
            _espnApi = espnApi;
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
                Uri = rootUrl,
                SourceUrlHash = HashProvider.GenerateHashFromUri(rootUrl),
                Name = Guid.NewGuid().ToString(),
                EndpointMask = null,
                IsSeasonSpecific = false
            };

            _resourceIndexId = resourceIndex.Id;

            // 2. Begin traversal
            //await TraverseAsync(rootUrl, null, 0, maxDepth);
            await TraverseFromFirstInstance(rootUrl, maxDepth);

            var outputToShowBit = _items.Select(x => new { x.Depth, Url = x.Uri}).ToJson();

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
            var listResult = await _espnApi.GetResource(listUrl);
            if (!listResult.IsSuccess)
            {
                _logger.LogError("Failed to fetch list: Status={Status}, Url={Url}", listResult.Status, listUrl);
                return;
            }
            
            var listDoc = JsonDocument.Parse(listResult.Value);

            var firstInstanceRef = ExtractRefs(listDoc).FirstOrDefault();
            if (firstInstanceRef == null)
            {
                _logger.LogWarning("No instance found in resource index {Uri}", listUrl);
                return;
            }

            var rootInstanceUrl = new Uri(firstInstanceRef);
            await TraverseOnlyResourceIndexes(rootInstanceUrl, depth: 1, maxDepth);
        }

        private async Task TraverseOnlyResourceIndexes(Uri instanceUrl, int depth, int maxDepth)
        {
            var result = await _espnApi.GetResource(instanceUrl);
            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to fetch instance: Status={Status}, Url={Url}", result.Status, instanceUrl);
                return;
            }
            
            var doc = JsonDocument.Parse(result.Value);
            var refs = ExtractRefs(doc);

            foreach (var href in refs)
            {
                if (!Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
                {
                    _logger.LogWarning("Skipping invalid URI: {Href}", href);
                    continue;
                }

                if (await IsEspnResourceIndex(uri))
                {
                    var hash = HashProvider.GenerateHashFromUri(new Uri(href));
                    if (_visited.TryAdd(hash, true))
                    {
                        _items.Add(new ResourceIndexItem
                        {
                            Id = Guid.NewGuid(),
                            ResourceIndexId = _resourceIndexId,
                            Uri = new Uri(href),
                            SourceUrlHash = hash,
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
                _logger.LogDebug("Max depth {Depth} reached at {Uri}", depth, url);
                return;
            }

            var urlHash = HashProvider.GenerateHashFromUri(url);
            if (!_visited.TryAdd(urlHash, true))
            {
                _logger.LogDebug("Already visited {Uri}", url);
                return;
            }

            try
            {
                var result = await _espnApi.GetResource(url);
                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Failed to fetch instance tree: Status={Status}, Url={Url}", result.Status, url);
                    return;
                }

                _items.Add(new ResourceIndexItem
                {
                    Id = Guid.NewGuid(),
                    ResourceIndexId = _resourceIndexId,
                    Uri = url,
                    SourceUrlHash = urlHash,
                    ParentItemId = parentItemId,
                    CreatedUtc = DateTime.UtcNow,
                    Depth = depth
                });

                var doc = JsonDocument.Parse(result.Value);
                var refs = ExtractRefs(doc);

                foreach (var href in refs)
                {
                    if (Uri.TryCreate(href, UriKind.RelativeOrAbsolute, out var uri))
                    {
                        await Task.Delay(250);

                        if (await IsEspnResourceIndex(uri))
                        {
                            var indexUrl = new Uri(href);
                            await TraverseInstanceTree(indexUrl, parentItemId: null, depth + 1, maxDepth);
                            //break; // Only recurse into the *first* valid index
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error traversing instance at {Uri}", url);
            }
        }
        private async Task<bool> IsEspnResourceIndex(Uri uri)
        {
            try
            {
                var result = await _espnApi.GetResource(uri);
                if (!result.IsSuccess)
                {
                    return false;
                }
                
                var parsed = JsonSerializer.Deserialize<EspnResourceIndexDto>(result.Value, new JsonSerializerOptions
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
        //        _logger.LogDebug("Max depth {MaxDepth} reached at {Uri}", maxDepth, url);
        //        return;
        //    }

        //    var urlHash = _hashProvider.GenerateHashFromUri(url.ToString());

        //    if (!_visited.TryAdd(urlHash, true))
        //    {
        //        _logger.LogDebug("Already visited {Uri}", url);
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
        //            Uri = url.ToString(),
        //            SourceUrlHash = urlHash,
        //            ParentItemId = parentItemId,
        //            CreatedUtc = DateTime.UtcNow,
        //            Depth = depth
        //        };

        //        _items.Add(item);

        //        var doc = JsonDocument.Parse(rawJson);

        //        var extractedRefs = ExtractRefs(doc).ToList();

        //        if (!extractedRefs.Any())
        //        {
        //            _logger.LogInformation("No references found in {Uri}", url);
        //            return;
        //        }

        //        _logger.LogInformation("Extracted {RefCount} references from {Uri}", extractedRefs.Count(), url);

        //        foreach (var href in extractedRefs)
        //        {
        //            await Task.Delay(250);
        //            await TraverseAsync(new Uri(href), item.Id, depth + 1, maxDepth);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Failed to fetch or process {Uri}", url);
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
