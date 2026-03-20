using System.Diagnostics.Metrics;
using System.Linq.Expressions;

using Microsoft.Extensions.Configuration;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Application.Services;
using SportsData.Provider.Infrastructure.Data;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Processors;

public class ResourceIndexItemProcessorTests : ProviderTestBase<ResourceIndexItemProcessor>
{
    private static readonly string ExistingJson = """{"id":"1","name":"existing"}""";
    private static readonly string UpdatedJson  = """{"id":"1","name":"updated"}""";
    private static readonly Uri   TestUri       = new("http://sports.core.api.espn.com/v2/sports/football/seasons/2017");
    private static readonly string UrlHash       = HashProvider.GenerateHashFromUri(TestUri);

    private static ProcessResourceIndexItemCommand BuildCommand(
        bool bypassCache,
        int? seasonYear = 2017) =>
        new(CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid(),
            MessageId: Guid.NewGuid(),
            ResourceIndexId: Guid.Empty,
            Id: UrlHash,
            Uri: TestUri,
            Sport: Sport.FootballNcaa,
            SourceDataProvider: SourceDataProvider.Espn,
            DocumentType: DocumentType.Season,
            ParentId: null,
            SeasonYear: seasonYear,
            BypassCache: bypassCache);

    private static DocumentBase ExistingDocument(string? lastPublishedContentHash = null) => new()
    {
        Id            = UrlHash,
        Data          = ExistingJson,
        Sport         = Sport.FootballNcaa,
        DocumentType  = DocumentType.Season,
        SourceDataProvider = SourceDataProvider.Espn,
        SourceUrlHash = UrlHash,
        Uri           = TestUri,
        RoutingKey    = UrlHash[..3].ToUpperInvariant(),
        LastPublishedContentHash = lastPublishedContentHash
    };

    private void SetupCommonMocks(int currentSeason = 2025)
    {
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());

        Mocker.GetMock<IMeterFactory>()
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns(new Meter("test"));

        Mocker.GetMock<IDocumentInclusionService>()
            .Setup(x => x.GetIncludableJson(It.IsAny<string>()))
            .Returns((string s) => s);

        Mocker.GetMock<IConfiguration>()
            .Setup(x => x["CommonConfig:ProviderClientConfig:ApiUrl"])
            .Returns("http://localhost:5000/");

        Mocker.GetMock<IConfiguration>()
            .Setup(x => x["CommonConfig:CurrentSeason"])
            .Returns(currentSeason.ToString());
    }

    #region BypassCache=true (ESPN fetch path)

    [Fact]
    public async Task WhenBypassCacheTrue_AndEspnReturnsUnchangedContent_ShouldNotReplaceInMongo_AndStillPublishDocumentCreated()
    {
        SetupCommonMocks();

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument());

        Mocker.GetMock<IProvideEspnApiData>()
            .Setup(x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(new Success<string>(ExistingJson));

        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(It.IsAny<string>()))
            .Returns("hash-unchanged");

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: true));

        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(
                x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Once,
                "BypassCache=true should fetch from ESPN before deciding whether to replace");

        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.ReplaceOneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DocumentBase>()),
                Times.Never,
                "unchanged content must not trigger a Mongo replace");

        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once,
                "DocumentCreated must be published even when Mongo replace is skipped");

        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.UpdateFieldAsync<DocumentBase>(
                    It.IsAny<string>(), It.IsAny<string>(),
                    nameof(DocumentBase.LastPublishedContentHash), It.IsAny<object?>()),
                Times.Once,
                "LastPublishedContentHash must be updated after publish");
    }

    [Fact]
    public async Task WhenBypassCacheTrue_AndEspnReturnsChangedContent_ShouldReplaceInMongo_AndPublishDocumentCreated()
    {
        SetupCommonMocks();

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument());

        Mocker.GetMock<IProvideEspnApiData>()
            .Setup(x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(new Success<string>(UpdatedJson));

        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(UpdatedJson))
            .Returns("hash-new");
        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(ExistingJson))
            .Returns("hash-old");

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.ReplaceOneAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DocumentBase>()))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: true));

        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(
                x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Once);

        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.ReplaceOneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DocumentBase>()),
                Times.Once,
                "changed content must trigger a Mongo replace");

        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once);
    }

    #endregion

    #region BypassCache=false, cache hit — publish suppression

    [Fact]
    public async Task WhenCacheHit_AndNoLastPublishedHash_ShouldPublishAndSetHash()
    {
        // First time a historical document is served from cache — LastPublishedContentHash is null
        SetupCommonMocks();

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument(lastPublishedContentHash: null));

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: false));

        // Should publish — no previous hash means never published
        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once);

        // Should update the hash after publishing
        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.UpdateFieldAsync<DocumentBase>(
                    It.IsAny<string>(), UrlHash,
                    nameof(DocumentBase.LastPublishedContentHash), It.IsAny<object?>()),
                Times.Once);

        // Should not call ESPN
        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(
                x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never);
    }

    [Fact]
    public async Task WhenCacheHit_Historical_AndHashMatches_ShouldSuppressPublish()
    {
        // Historical document with matching hash — suppress
        SetupCommonMocks(currentSeason: 2025);

        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(ExistingJson))
            .Returns("hash-existing");

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument(lastPublishedContentHash: "hash-existing"));

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: false, seasonYear: 2017));

        // Should NOT publish — content unchanged since last publish
        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Never,
                "historical document with matching hash must be suppressed");

        // Should NOT update the hash (nothing was published)
        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.UpdateFieldAsync<DocumentBase>(
                    It.IsAny<string>(), It.IsAny<string>(),
                    nameof(DocumentBase.LastPublishedContentHash), It.IsAny<object?>()),
                Times.Never);
    }

    [Fact]
    public async Task WhenCacheHit_Historical_AndHashDiffers_ShouldPublish()
    {
        // Historical document where content changed (hash mismatch) — must publish
        SetupCommonMocks(currentSeason: 2025);

        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(ExistingJson))
            .Returns("hash-current");

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument(lastPublishedContentHash: "hash-stale"));

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: false, seasonYear: 2017));

        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once,
                "historical document with changed content must be published");

        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.UpdateFieldAsync<DocumentBase>(
                    It.IsAny<string>(), UrlHash,
                    nameof(DocumentBase.LastPublishedContentHash), It.IsAny<object?>()),
                Times.Once);
    }

    [Fact]
    public async Task WhenCacheHit_CurrentSeason_AndHashMatches_ShouldStillPublish()
    {
        // Current-season document — always publish even if hash matches
        SetupCommonMocks(currentSeason: 2025);

        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(ExistingJson))
            .Returns("hash-existing");

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument(lastPublishedContentHash: "hash-existing"));

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        // SeasonYear 2025 == CurrentSeason → current season
        await sut.Process(BuildCommand(bypassCache: false, seasonYear: 2025));

        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once,
                "current-season documents must always be published regardless of hash");
    }

    [Fact]
    public async Task WhenCacheHit_NullSeasonYear_AndHashMatches_ShouldStillPublish()
    {
        // Non-seasonal resource (Venue, Franchise) — always publish
        SetupCommonMocks(currentSeason: 2025);

        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(ExistingJson))
            .Returns("hash-existing");

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument(lastPublishedContentHash: "hash-existing"));

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: false, seasonYear: null));

        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once,
                "non-seasonal resources must always be published regardless of hash");
    }

    [Fact]
    public async Task WhenCacheHit_CurrentSeasonZero_AndHashMatches_ShouldStillPublish()
    {
        // CurrentSeason=0 means feature disabled — always publish
        SetupCommonMocks(currentSeason: 0);

        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(ExistingJson))
            .Returns("hash-existing");

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument(lastPublishedContentHash: "hash-existing"));

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: false, seasonYear: 2017));

        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once,
                "CurrentSeason=0 disables suppression — must always publish");
    }

    #endregion

    #region BypassCache=false, cache hit — original behavior preserved

    [Fact]
    public async Task WhenBypassCacheFalse_AndDocumentExistsInMongo_ShouldNotCallEspn_AndPublishDocumentCreated()
    {
        // Original test — cache hit with no suppression (no LastPublishedContentHash)
        SetupCommonMocks();

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument());

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        await sut.Process(BuildCommand(bypassCache: false));

        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(
                x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never,
                "cached document must be served without an ESPN call when BypassCache=false");

        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.ReplaceOneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DocumentBase>()),
                Times.Never,
                "cache-hit path should not replace an unchanged existing document");

        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once);
    }

    #endregion
}
