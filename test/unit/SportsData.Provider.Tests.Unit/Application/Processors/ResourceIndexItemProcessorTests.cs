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

    private static ProcessResourceIndexItemCommand BuildCommand(bool bypassCache) =>
        new(CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid(),
            MessageId: Guid.NewGuid(),
            ResourceIndexId: Guid.Empty, // skip ResourceIndexItem DB tracking
            Id: UrlHash,
            Uri: TestUri,
            Sport: Sport.FootballNcaa,
            SourceDataProvider: SourceDataProvider.Espn,
            DocumentType: DocumentType.Season,
            ParentId: null,
            SeasonYear: 2017,
            BypassCache: bypassCache);

    private static DocumentBase ExistingDocument() => new()
    {
        Id            = UrlHash,
        Data          = ExistingJson,
        Sport         = Sport.FootballNcaa,
        DocumentType  = DocumentType.Season,
        SourceDataProvider = SourceDataProvider.Espn,
        SourceUrlHash = UrlHash,
        Uri           = TestUri,
        RoutingKey    = UrlHash[..3].ToUpperInvariant()
    };

    private void SetupCommonMocks()
    {
        // Use real identity generator so URI → CanonicalId/UrlHash resolution works
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());

        Mocker.GetMock<IDocumentInclusionService>()
            .Setup(x => x.GetIncludableJson(It.IsAny<string>()))
            .Returns((string s) => s);

        Mocker.GetMock<IConfiguration>()
            .Setup(x => x["CommonConfig:ProviderClientConfig:ApiUrl"])
            .Returns("http://localhost:5000/");
    }

    /// <summary>
    /// Core regression: BypassCache=true used to force a Mongo replace unconditionally
    /// (via `|| command.BypassCache` in the write-back condition) even when ESPN returned
    /// the exact same content that was already stored. The replace must be skipped when
    /// content is unchanged; downstream processing must still continue via DocumentCreated.
    /// </summary>
    [Fact]
    public async Task WhenBypassCacheTrue_AndEspnReturnsUnchangedContent_ShouldNotReplaceInMongo_AndStillPublishDocumentCreated()
    {
        // Arrange
        SetupCommonMocks();

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument());

        Mocker.GetMock<IProvideEspnApiData>()
            .Setup(x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(new Success<string>(ExistingJson));

        // Both old and new content hash to the same value — content has not changed
        Mocker.GetMock<IJsonHashCalculator>()
            .Setup(x => x.NormalizeAndHash(It.IsAny<string>()))
            .Returns("hash-unchanged");

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        // Act
        await sut.Process(BuildCommand(bypassCache: true));

        // Assert — ESPN must have been called (BypassCache=true skips the Mongo read-cache path)
        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(
                x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Once,
                "BypassCache=true should fetch from ESPN before deciding whether to replace");

        // Assert — Mongo replace must NOT have been called
        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.ReplaceOneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DocumentBase>()),
                Times.Never,
                "unchanged content must not trigger a Mongo replace");

        // Assert — DocumentCreated must still be published so downstream processing continues
        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once,
                "DocumentCreated must be published even when Mongo replace is skipped");
    }

    [Fact]
    public async Task WhenBypassCacheTrue_AndEspnReturnsChangedContent_ShouldReplaceInMongo_AndPublishDocumentCreated()
    {
        // Arrange
        SetupCommonMocks();

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument());

        Mocker.GetMock<IProvideEspnApiData>()
            .Setup(x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(new Success<string>(UpdatedJson));

        // Distinct hashes — content has genuinely changed
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

        // Act
        await sut.Process(BuildCommand(bypassCache: true));

        // Assert — ESPN must have been called (BypassCache=true skips the Mongo read-cache path)
        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(
                x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Once,
                "BypassCache=true should fetch from ESPN for changed-content evaluation");

        // Assert — Mongo replace must have been called once
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

    [Fact]
    public async Task WhenBypassCacheFalse_AndDocumentExistsInMongo_ShouldNotCallEspn_AndPublishDocumentCreated()
    {
        // Arrange — BypassCache=false: the cache-hit path should serve the document and return early
        SetupCommonMocks();

        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetFirstOrDefaultAsync<DocumentBase>(
                It.IsAny<string>(),
                It.IsAny<Expression<Func<DocumentBase, bool>>>()))
            .ReturnsAsync(ExistingDocument());

        var sut = Mocker.CreateInstance<ResourceIndexItemProcessor>();

        // Act
        await sut.Process(BuildCommand(bypassCache: false));

        // Assert — ESPN must not have been called
        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(
                x => x.GetResource(It.IsAny<Uri>(), It.IsAny<bool>(), It.IsAny<bool>()),
                Times.Never,
                "cached document must be served without an ESPN call when BypassCache=false");

        // Assert — cache-hit must not rewrite Mongo
        Mocker.GetMock<IDocumentStore>()
            .Verify(
                x => x.ReplaceOneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DocumentBase>()),
                Times.Never,
                "cache-hit path should not replace an unchanged existing document");

        // Assert — DocumentCreated published from cache
        Mocker.GetMock<IEventBus>()
            .Verify(
                x => x.Publish(It.IsAny<DocumentCreated>(), default),
                Times.Once);
    }
}
