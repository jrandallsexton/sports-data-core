using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    public class EventCompetitionOddsDocumentProcessorTests
        : ProducerTestBase<EventCompetitionOddsDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task Test_Deserialization()
        {
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");
            var dto = documentJson.FromJson<EspnEventCompetitionOddsDto>();
            dto.Should().NotBeNull();
        }

        [Fact]
        public async Task EspnEventCompetitionOddsDto_Deserializes_AllFieldsCorrectly()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");

            // Act
            var actual = json.FromJson<EspnEventCompetitionOddsDto>();

            // Assert root
            actual.Should().NotBeNull();
            actual.Ref.Should().NotBeNull();
            actual.Ref.AbsoluteUri.Should().Contain("/odds/58");

            actual.Details.Should().Be("LSU -3.5");
            actual.OverUnder.Should().Be(67.5m);
            actual.Spread.Should().Be(-3.5m);
            actual.OverOdds.Should().Be(-110m);
            actual.UnderOdds.Should().Be(-110m);
            actual.MoneylineWinner.Should().BeFalse();
            actual.SpreadWinner.Should().BeFalse();

            // Provider
            actual.Provider.Should().NotBeNull();
            actual.Provider.Ref.Should().Contain("/providers/58");
            actual.Provider.Id.Should().Be("58");
            actual.Provider.Name.Should().Be("ESPN BET");
            actual.Provider.Priority.Should().Be(1);

            // AwayTeamOdds
            actual.AwayTeamOdds.Should().NotBeNull();
            actual.AwayTeamOdds.Favorite.Should().BeFalse();
            actual.AwayTeamOdds.Underdog.Should().BeTrue();
            actual.AwayTeamOdds.MoneyLine.Should().Be(145);
            actual.AwayTeamOdds.SpreadOdds.Should().Be(-120m);

            actual.AwayTeamOdds.Team.Should().NotBeNull();
            actual.AwayTeamOdds.Team.Ref.Should().Contain("/teams/30");

            // AwayTeam.Open
            var awayOpen = actual.AwayTeamOdds.Open;
            awayOpen.Should().NotBeNull();
            awayOpen.Favorite.Should().BeFalse();
            awayOpen.PointSpread.Should().NotBeNull();
            awayOpen.PointSpread.AlternateDisplayValue.Should().Be("+6");
            awayOpen.PointSpread.American.Should().Be("+6");

            awayOpen.Spread.Value.Should().Be(1.909m);
            awayOpen.Spread.DisplayValue.Should().Be("10/11");
            awayOpen.Spread.AlternateDisplayValue.Should().Be("-110");
            awayOpen.Spread.Decimal.Should().Be(1.909m);
            awayOpen.Spread.Fraction.Should().Be("10/11");
            awayOpen.Spread.American.Should().Be("-110");

            awayOpen.MoneyLine.Value.Should().Be(2.8m);
            awayOpen.MoneyLine.DisplayValue.Should().Be("9/5");
            awayOpen.MoneyLine.AlternateDisplayValue.Should().Be("+180");
            awayOpen.MoneyLine.Decimal.Should().Be(2.8m);
            awayOpen.MoneyLine.Fraction.Should().Be("9/5");
            awayOpen.MoneyLine.American.Should().Be("+180");

            // AwayTeam.Close
            var awayClose = actual.AwayTeamOdds.Close;
            awayClose.PointSpread.AlternateDisplayValue.Should().Be("+3.5");
            awayClose.PointSpread.American.Should().Be("+3.5");
            awayClose.Spread.Value.Should().Be(1.833m);
            awayClose.Spread.DisplayValue.Should().Be("5/6");
            awayClose.Spread.AlternateDisplayValue.Should().Be("-120");
            awayClose.Spread.Decimal.Should().Be(1.833m);
            awayClose.Spread.Fraction.Should().Be("5/6");
            awayClose.Spread.American.Should().Be("-120");
            awayClose.MoneyLine.Value.Should().Be(2.45m);
            awayClose.MoneyLine.DisplayValue.Should().Be("29/20");
            awayClose.MoneyLine.AlternateDisplayValue.Should().Be("+145");
            awayClose.MoneyLine.Decimal.Should().Be(2.45m);
            awayClose.MoneyLine.Fraction.Should().Be("29/20");
            awayClose.MoneyLine.American.Should().Be("+145");

            // AwayTeam.Current
            var awayCurrent = actual.AwayTeamOdds.Current;
            awayCurrent.PointSpread.AlternateDisplayValue.Should().Be("+3.5");
            awayCurrent.PointSpread.American.Should().Be("+3.5");
            awayCurrent.Spread.Value.Should().Be(1.833m);
            awayCurrent.Spread.DisplayValue.Should().Be("5/6");
            awayCurrent.Spread.AlternateDisplayValue.Should().Be("-120");
            awayCurrent.Spread.Decimal.Should().Be(1.833m);
            awayCurrent.Spread.Fraction.Should().Be("5/6");
            awayCurrent.Spread.American.Should().Be("-120");
            awayCurrent.Spread.Outcome.Type.Should().Be("win");
            awayCurrent.MoneyLine.Value.Should().Be(2.45m);
            awayCurrent.MoneyLine.DisplayValue.Should().Be("29/20");
            awayCurrent.MoneyLine.AlternateDisplayValue.Should().Be("+145");
            awayCurrent.MoneyLine.Decimal.Should().Be(2.45m);
            awayCurrent.MoneyLine.Fraction.Should().Be("29/20");
            awayCurrent.MoneyLine.American.Should().Be("+145");
            awayCurrent.MoneyLine.Outcome.Type.Should().Be("win");

            // HomeTeamOdds
            actual.HomeTeamOdds.Should().NotBeNull();
            actual.HomeTeamOdds.Favorite.Should().BeTrue();
            actual.HomeTeamOdds.Underdog.Should().BeFalse();
            actual.HomeTeamOdds.MoneyLine.Should().Be(-170);
            actual.HomeTeamOdds.SpreadOdds.Should().Be(100m);

            actual.HomeTeamOdds.Team.Should().NotBeNull();
            actual.HomeTeamOdds.Team.Ref.Should().Contain("/teams/99");

            // HomeTeam.Open
            var homeOpen = actual.HomeTeamOdds.Open;
            homeOpen.Should().NotBeNull();
            homeOpen.Favorite.Should().BeTrue();
            homeOpen.PointSpread.AlternateDisplayValue.Should().Be("-6");
            homeOpen.PointSpread.American.Should().Be("-6");
            homeOpen.Spread.Value.Should().Be(1.909m);
            homeOpen.Spread.DisplayValue.Should().Be("10/11");
            homeOpen.Spread.AlternateDisplayValue.Should().Be("-110");
            homeOpen.Spread.Decimal.Should().Be(1.909m);
            homeOpen.Spread.Fraction.Should().Be("10/11");
            homeOpen.Spread.American.Should().Be("-110");
            homeOpen.MoneyLine.Value.Should().Be(1.465m);
            homeOpen.MoneyLine.DisplayValue.Should().Be("20/43");
            homeOpen.MoneyLine.AlternateDisplayValue.Should().Be("-215");
            homeOpen.MoneyLine.Decimal.Should().Be(1.465m);
            homeOpen.MoneyLine.Fraction.Should().Be("20/43");
            homeOpen.MoneyLine.American.Should().Be("-215");

            // HomeTeam.Close
            var homeClose = actual.HomeTeamOdds.Close;
            homeClose.PointSpread.AlternateDisplayValue.Should().Be("-3.5");
            homeClose.PointSpread.American.Should().Be("-3.5");
            homeClose.Spread.Value.Should().Be(2.0m);
            homeClose.Spread.DisplayValue.Should().Be("1/1");
            homeClose.Spread.AlternateDisplayValue.Should().Be("EVEN");
            homeClose.Spread.Decimal.Should().Be(2.0m);
            homeClose.Spread.Fraction.Should().Be("1/1");
            homeClose.Spread.American.Should().Be("EVEN");
            homeClose.MoneyLine.Value.Should().Be(1.588m);
            homeClose.MoneyLine.DisplayValue.Should().Be("10/17");
            homeClose.MoneyLine.AlternateDisplayValue.Should().Be("-170");
            homeClose.MoneyLine.Decimal.Should().Be(1.588m);
            homeClose.MoneyLine.Fraction.Should().Be("10/17");
            homeClose.MoneyLine.American.Should().Be("-170");

            // HomeTeam.Current
            var homeCurrent = actual.HomeTeamOdds.Current;
            homeCurrent.PointSpread.AlternateDisplayValue.Should().Be("-3.5");
            homeCurrent.PointSpread.American.Should().Be("-3.5");
            homeCurrent.Spread.Value.Should().Be(2.0m);
            homeCurrent.Spread.DisplayValue.Should().Be("1/1");
            homeCurrent.Spread.AlternateDisplayValue.Should().Be("EVEN");
            homeCurrent.Spread.Decimal.Should().Be(2.0m);
            homeCurrent.Spread.Fraction.Should().Be("1/1");
            homeCurrent.Spread.American.Should().Be("EVEN");
            homeCurrent.Spread.Outcome.Type.Should().Be("loss");
            homeCurrent.MoneyLine.Value.Should().Be(1.588m);
            homeCurrent.MoneyLine.DisplayValue.Should().Be("10/17");
            homeCurrent.MoneyLine.AlternateDisplayValue.Should().Be("-170");
            homeCurrent.MoneyLine.Decimal.Should().Be(1.588m);
            homeCurrent.MoneyLine.Fraction.Should().Be("10/17");
            homeCurrent.MoneyLine.American.Should().Be("-170");
            homeCurrent.MoneyLine.Outcome.Type.Should().Be("loss");

            // Root-level Open
            actual.Open.Should().NotBeNull();
            actual.Open.Over.Value.Should().Be(1.909m);
            actual.Open.Under.Value.Should().Be(1.909m);
            actual.Open.Total.AlternateDisplayValue.Should().Be("62.5");

            // Root-level Close
            actual.Close.Should().NotBeNull();
            actual.Close.Total.AlternateDisplayValue.Should().Be("67.5");

            // Root-level Current
            actual.Current.Should().NotBeNull();
            actual.Current.Over.Outcome.Type.Should().Be("loss");
            actual.Current.Under.Outcome.Type.Should().Be("win");
            actual.Current.Total.AlternateDisplayValue.Should().Be("67.5");

            // Links
            actual.Links.Should().NotBeNullOrEmpty();
            actual.Links.Should().ContainSingle(l => l.Rel.Contains("home"));
            actual.Links.Should().ContainSingle(l => l.Rel.Contains("away"));
            actual.Links.Should().ContainSingle(l => l.Rel.Contains("over"));
            actual.Links.Should().ContainSingle(l => l.Rel.Contains("under"));
            actual.Links.Should().ContainSingle(l => l.Rel.Contains("game"));
        }
        
        [Fact]
        public async Task WhenNoExistingOdds_AddsNewOddsAndPublishesEvent()
        {
            // Arrange
            var bus = Mocker.GetMock<IPublishEndpoint>();
            var sut = Mocker.CreateInstance<EventCompetitionOddsDocumentProcessor<FootballDataContext>>();

            var contestId = Guid.NewGuid();

            var existingContest = Fixture.Build<Contest>()
                .WithAutoProperties()
                .With(x => x.Id, contestId)
                .With(x => x.HomeTeamFranchiseSeasonId, Guid.NewGuid())
                .With(x => x.AwayTeamFranchiseSeasonId, Guid.NewGuid())
                .With(x => x.Odds, new List<ContestOdds>())
                .Create();

            await FootballDataContext.Contests.AddAsync(existingContest);
            await FootballDataContext.SaveChangesAsync();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, contestId.ToString())
                .With(x => x.Season, 2025)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
                .With(x => x.Document, documentJson)
                .OmitAutoProperties()
                .Create();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var contest = await FootballDataContext.Contests
                .Include(c => c.Odds)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            contest.Odds.Should().HaveCount(1);
            bus.Verify(x => x.Publish(It.IsAny<ContestOddsCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WhenExistingOddsWithSameProvider_AndIdentical_NoNewOddsNoEvent()
        {
            // Arrange
            var bus = Mocker.GetMock<IPublishEndpoint>();
            var sut = Mocker.CreateInstance<EventCompetitionOddsDocumentProcessor<FootballDataContext>>();

            var contestId = Guid.NewGuid();
            var homeId = Guid.NewGuid();
            var awayId = Guid.NewGuid();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");

            // Build command
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, contestId.ToString())
                .With(x => x.Season, 2025)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
                .With(x => x.Document, documentJson)
                .OmitAutoProperties()
                .Create();

            // Deserialize odds and add to contest
            var dto = documentJson.FromJson<EspnEventCompetitionOddsDto>();
            var existingOdds = dto.AsEntity(contestId, homeId, awayId);

            var existingContest = Fixture.Build<Contest>()
                .WithAutoProperties()
                .With(x => x.Id, contestId)
                .With(x => x.HomeTeamFranchiseSeasonId, homeId)
                .With(x => x.AwayTeamFranchiseSeasonId, awayId)
                .With(x => x.Odds, new List<ContestOdds> { existingOdds })
                .Create();

            await FootballDataContext.Contests.AddAsync(existingContest);
            await FootballDataContext.SaveChangesAsync();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var contest = await FootballDataContext.Contests
                .Include(c => c.Odds)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            contest.Odds.Should().HaveCount(1);
            bus.Verify(x => x.Publish(It.IsAny<ContestOddsCreated>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WhenExistingOddsWithSameProvider_ButDifferent_AddsNewOddsAndPublishesEvent()
        {
            // Arrange
            var bus = Mocker.GetMock<IPublishEndpoint>();
            var sut = Mocker.CreateInstance<EventCompetitionOddsDocumentProcessor<FootballDataContext>>();

            var contestId = Guid.NewGuid();
            var homeId = Guid.NewGuid();
            var awayId = Guid.NewGuid();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");

            // Add *original* odds to DB
            var dtoOriginal = documentJson.FromJson<EspnEventCompetitionOddsDto>();
            var existingOdds = dtoOriginal.AsEntity(contestId, homeId, awayId);

            var existingContest = Fixture.Build<Contest>()
                .WithAutoProperties()
                .With(x => x.Id, contestId)
                .With(x => x.HomeTeamFranchiseSeasonId, homeId)
                .With(x => x.AwayTeamFranchiseSeasonId, awayId)
                .With(x => x.Odds, [existingOdds])
                .Create();

            await FootballDataContext.Contests.AddAsync(existingContest);
            await FootballDataContext.SaveChangesAsync();

            // Now create *incoming* odds with changed field
            var dtoForCommand = documentJson.FromJson<EspnEventCompetitionOddsDto>();
            dtoForCommand.Details = "DIFFERENT";
            var modifiedDocumentJson = dtoForCommand.ToJson();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, contestId.ToString())
                .With(x => x.Season, 2025)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
                .With(x => x.Document, modifiedDocumentJson)
                .OmitAutoProperties()
                .Create();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var contest = await FootballDataContext.Contests
                .Include(c => c.Odds)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            contest.Odds.Should().HaveCount(2);
            bus.Verify(x => x.Publish(It.IsAny<ContestOddsCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        }

    }
}
