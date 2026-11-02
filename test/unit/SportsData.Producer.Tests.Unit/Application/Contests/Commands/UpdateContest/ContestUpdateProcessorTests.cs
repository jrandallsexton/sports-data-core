#nullable enable

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.Contests.Commands.UpdateContest;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Commands.UpdateContest;

public class ContestUpdateProcessorTests :
    ProducerTestBase<ContestUpdateProcessor<FootballDataContext>>
{
    private const string EventUrl =
        "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752677";

    private async Task<Guid> SeedContestAsync()
    {
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var identity = generator.Generate(EventUrl);

        var contestId = Guid.NewGuid();
        await FootballDataContext.Contests.AddAsync(new FootballContest
        {
            Id = contestId,
            Name = "Away at Home",
            ShortName = "AWY @ HOM",
            StartDateUtc = new DateTime(2026, 9, 5, 23, 0, 0, DateTimeKind.Utc),
            Sport = Sport.FootballNcaa,
            SeasonYear = 2026,
            CreatedUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new ContestExternalId
                {
                    Id = Guid.NewGuid(),
                    ContestId = contestId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash,
                    Value = identity.UrlHash
                }
            ]
        });
        await FootballDataContext.SaveChangesAsync();

        return contestId;
    }

    [Fact]
    public async Task Process_WithoutOverride_PublishesStandardRefreshSet()
    {
        // arrange
        var contestId = await SeedContestAsync();
        var sut = Mocker.CreateInstance<ContestUpdateProcessor<FootballDataContext>>();

        // act
        await sut.Process(new UpdateContestCommand(
            contestId, SourceDataProvider.Espn, Sport.FootballNcaa, Guid.NewGuid()));

        // assert
        Mocker.GetMock<IEventBus>().Verify(x => x.Publish(
            It.Is<DocumentRequested>(d =>
                d.DocumentType == DocumentType.Event &&
                d.IncludeLinkedDocumentTypes != null &&
                d.IncludeLinkedDocumentTypes.SequenceEqual(UpdateContestCommand.DefaultIncludeLinkedDocumentTypes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WithOverride_PublishesOnlyTheNarrowedTypes()
    {
        // The curated-rank backfill re-sources competitors only: no plays,
        // drives, odds or probabilities.

        // arrange
        var contestId = await SeedContestAsync();
        var sut = Mocker.CreateInstance<ContestUpdateProcessor<FootballDataContext>>();
        List<DocumentType> narrowed =
        [
            DocumentType.EventCompetition,
            DocumentType.EventCompetitionCompetitor
        ];

        // act
        await sut.Process(new UpdateContestCommand(
            contestId, SourceDataProvider.Espn, Sport.FootballNcaa, Guid.NewGuid(), narrowed));

        // assert
        Mocker.GetMock<IEventBus>().Verify(x => x.Publish(
            It.Is<DocumentRequested>(d =>
                d.DocumentType == DocumentType.Event &&
                d.IncludeLinkedDocumentTypes != null &&
                d.IncludeLinkedDocumentTypes.SequenceEqual(narrowed)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
