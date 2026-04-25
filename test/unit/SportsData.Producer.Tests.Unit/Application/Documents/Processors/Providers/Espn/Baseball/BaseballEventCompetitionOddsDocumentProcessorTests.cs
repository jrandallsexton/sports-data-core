#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// Tests for BaseballEventCompetitionOddsDocumentProcessor — MLB-specific
/// processor that handles the paged-collection wrapper ESPN serves on
/// <c>/competitions/{id}/odds</c> for baseball.
///
/// Uses <see cref="FootballDataContext"/> as the concrete TDataContext —
/// the processor is generic on TeamSportDataContext, and other Baseball
/// processor tests follow the same pragmatic pattern until a dedicated
/// BaseballDataContext test scaffold lands.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionOddsDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionOddsDocumentProcessor<FootballDataContext>>
{
    private async Task<(ContestBase contest, CompetitionBase competition)> CreateTestContestAndCompetitionAsync(Guid competitionId)
    {
        var contest = new FootballContest
        {
            Id = Guid.NewGuid(),
            Name = "Test Contest",
            ShortName = "Test",
            SeasonYear = 2026,
            Sport = Sport.BaseballMlb,
            StartDateUtc = DateTime.UtcNow,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = contest.Id,
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        return (contest, competition);
    }

    [Fact]
    public async Task EspnEventCompetitionOddsListDto_DeserializesMlbWrapper()
    {
        // arrange
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionOdds.json");

        // act
        var wrapper = json.FromJson<EspnEventCompetitionOddsListDto>();

        // assert — pagination metadata present
        wrapper.Should().NotBeNull();
        wrapper!.Count.Should().Be(1);
        wrapper.PageIndex.Should().Be(1);
        wrapper.PageCount.Should().Be(1);
        wrapper.Items.Should().HaveCount(1);

        // assert — items[0] populates the per-provider DTO correctly
        var item = wrapper.Items[0];
        item.Ref.Should().BeNull("MLB items have no $ref of their own — this is the whole reason for the sport-specific processor");
        item.Provider.Should().NotBeNull();
        item.Provider!.Id.Should().Be("100");
        item.Provider.Name.Should().Be("DraftKings");
        item.Details.Should().Be("CLE -122");
        item.OverUnder.Should().Be(6.0m);
        item.Spread.Should().Be(1.5m);
        item.AwayTeamOdds.Should().NotBeNull();
        item.AwayTeamOdds!.Favorite.Should().BeTrue();
        item.AwayTeamOdds.MoneyLine.Should().Be(102);
        item.HomeTeamOdds.Should().NotBeNull();
        item.HomeTeamOdds!.MoneyLine.Should().Be(-122);
        item.Links.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task WhenNoExistingOdds_AddsOneRowPerItem_AndPublishesCreated()
    {
        // arrange
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
        var hash = Mocker.GetMock<IJsonHashCalculator>();

        var compId = Guid.NewGuid();
        await CreateTestContestAndCompetitionAsync(compId);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionOdds.json");

        // Capture every URI Generate is called with so we can assert that
        // synthetic-$ref derivation produced the correct shape.
        var generatedUris = new List<Uri>();
        idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
            .Callback<Uri>(u => generatedUris.Add(u))
            .Returns(() => new ExternalRefIdentity(Guid.NewGuid(), $"hash-{generatedUris.Count}", "http://x/clean"));

        hash.Setup(x => x.NormalizeAndHash(It.IsAny<string>())).Returns("content-hash-v1");

        var listingUri = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/odds?lang=en&region=us");

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, compId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
            .With(x => x.Document, json)
            .With(x => x.SourceUri, listingUri)
            .With(x => x.UrlHash, "url-hash")
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionOddsDocumentProcessor<FootballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — fixture has exactly 1 item; one CompetitionOdds row written.
        var saved = await FootballDataContext.CompetitionOdds.AsNoTracking().ToListAsync();
        saved.Should().HaveCount(1);
        saved[0].CompetitionId.Should().Be(compId);
        saved[0].ContentHash.Should().Be("content-hash-v1");

        // assert — synthetic ref uses the listing URL + #provider={id}
        // The synthetic ref must place provider id in the PATH, not the
        // query/fragment. UriExtensions.ToCleanUrl strips query+fragment,
        // so any provider distinction outside the path collapses every
        // item onto the same canonical id during identity generation.
        generatedUris.Should().NotBeEmpty();
        generatedUris.Should().Contain(u =>
            u.AbsolutePath.Contains("/odds/provider/100"));

        // assert — created event published (no prior odds existed)
        bus.Verify(x => x.Publish(It.IsAny<ContestOddsCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(x => x.Publish(It.IsAny<ContestOddsUpdated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenContentHashUnchanged_SkipsWrite_DoesNotPublish()
    {
        // arrange
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
        var hash = Mocker.GetMock<IJsonHashCalculator>();

        var compId = Guid.NewGuid();
        await CreateTestContestAndCompetitionAsync(compId);

        // Pre-seed an existing CompetitionOdds row with the SAME content hash
        // the processor will compute. The skip-when-unchanged check should
        // bail before any new rows are added or events published.
        var preExisting = new CompetitionOdds
        {
            Id = Guid.NewGuid(),
            CompetitionId = compId,
            ProviderRef = new Uri("http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/odds#provider=100"),
            ProviderId = "100",
            ProviderName = "DraftKings",
            ProviderPriority = 1,
            ContentHash = "content-hash-v1",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionOdds.AddAsync(preExisting);
        await FootballDataContext.SaveChangesAsync();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionOdds.json");

        hash.Setup(x => x.NormalizeAndHash(It.IsAny<string>())).Returns("content-hash-v1");

        var listingUri = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/odds?lang=en&region=us");

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, compId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
            .With(x => x.Document, json)
            .With(x => x.SourceUri, listingUri)
            .With(x => x.UrlHash, "url-hash")
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionOddsDocumentProcessor<FootballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — exactly one row remains (the pre-existing one); identity unchanged
        var saved = await FootballDataContext.CompetitionOdds.AsNoTracking().ToListAsync();
        saved.Should().HaveCount(1);
        saved[0].Id.Should().Be(preExisting.Id);

        // assert — no events emitted
        bus.Verify(x => x.Publish(It.IsAny<ContestOddsCreated>(), It.IsAny<CancellationToken>()), Times.Never);
        bus.Verify(x => x.Publish(It.IsAny<ContestOddsUpdated>(), It.IsAny<CancellationToken>()), Times.Never);

        // assert — no synthetic refs were generated (we bailed before the per-item loop)
        idGen.Verify(x => x.Generate(It.IsAny<Uri>()), Times.Never);
    }

    [Fact]
    public async Task WhenContentHashChanged_ReplacesExistingRows_PublishesUpdated()
    {
        // arrange
        var bus = Mocker.GetMock<IEventBus>();
        var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
        var hash = Mocker.GetMock<IJsonHashCalculator>();

        var compId = Guid.NewGuid();
        await CreateTestContestAndCompetitionAsync(compId);

        // Pre-seed with an OLD content hash so the wrapper-vs-existing comparison
        // detects a change and triggers the replace path.
        var oldRowId = Guid.NewGuid();
        var preExisting = new CompetitionOdds
        {
            Id = oldRowId,
            CompetitionId = compId,
            ProviderRef = new Uri("http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/odds#provider=100"),
            ProviderId = "100",
            ProviderName = "DraftKings",
            ProviderPriority = 1,
            ContentHash = "content-hash-OLD",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionOdds.AddAsync(preExisting);
        await FootballDataContext.SaveChangesAsync();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionOdds.json");

        idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
            .Returns(new ExternalRefIdentity(Guid.NewGuid(), "hash-new", "http://x/clean"));
        hash.Setup(x => x.NormalizeAndHash(It.IsAny<string>())).Returns("content-hash-NEW");

        var listingUri = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/odds?lang=en&region=us");

        var cmd = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, compId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
            .With(x => x.Document, json)
            .With(x => x.SourceUri, listingUri)
            .With(x => x.UrlHash, "url-hash")
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionOddsDocumentProcessor<FootballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — old row gone, fresh row written with the new hash
        var saved = await FootballDataContext.CompetitionOdds.AsNoTracking().ToListAsync();
        saved.Should().HaveCount(1);
        saved[0].Id.Should().NotBe(oldRowId);
        saved[0].ContentHash.Should().Be("content-hash-NEW");

        // assert — Updated event (not Created) since rows existed before
        bus.Verify(x => x.Publish(It.IsAny<ContestOddsUpdated>(), It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(x => x.Publish(It.IsAny<ContestOddsCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
