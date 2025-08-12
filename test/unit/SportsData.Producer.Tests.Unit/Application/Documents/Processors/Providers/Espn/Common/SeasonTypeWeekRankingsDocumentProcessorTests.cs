using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestPlatform.Utilities;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    public class SeasonTypeWeekRankingsDocumentProcessorTests
    : ProducerTestBase<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenJsonIsValid_DtoDeserializes()
        {
            // arrange
            var json = await LoadJsonTestData("EspnFootballNcaaSeasonTypeWeekRankings.json");

            // act
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            // assert
            dto.Should().NotBeNull();

            // factual assertions based on the test JSON
            dto.Id.Should().Be("2");
            dto.Name.Should().Be("AFCA Coaches Poll");
            dto.Season.Should().NotBeNull();
            dto.Season.Year.Should().Be(2025);

            dto.Ranks.Should().HaveCount(25);

            var firstRank = dto.Ranks.First();
            firstRank.Current.Should().Be(1);
            firstRank.Previous.Should().Be(0);
            firstRank.Points.Should().Be(1606.0);
            firstRank.FirstPlaceVotes.Should().Be(28);
            firstRank.Trend.Should().Be("-");

            firstRank.Record.Should().NotBeNull();
            firstRank.Record.Summary.Should().Be("0-0");

            firstRank.Record.Stats.Should().ContainSingle(s => s.Name == "wins" && s.Value == 0.0);
            firstRank.Record.Stats.Should().ContainSingle(s => s.Name == "losses" && s.Value == 0.0);

            firstRank.Team.Ref.Should().Be("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/251?lang=en&region=us");

            firstRank.Date.Should().Be("2025-08-04T07:00Z");
            firstRank.LastUpdated.Should().Be("2025-08-04T19:24Z");
        }

        [Fact]
        public async Task WhenDtoIsValid_EntityIsCreated()
        {
            // arrange
            var json = await LoadJsonTestData("EspnFootballNcaaSeasonTypeWeekRankings.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            var seasonWeekId = Guid.NewGuid();
            var generator = new ExternalRefIdentityGenerator();
            var correlationId = Guid.NewGuid();
            Dictionary<string, Guid> franchiseDictionary = new();

            var expectedIdentity = generator.Generate(dto.Ref!);

            // act
            var before = DateTime.UtcNow;
            var entity = dto.AsEntity(seasonWeekId, generator, franchiseDictionary, correlationId);
            var after = DateTime.UtcNow;

            // assert: top-level entity
            entity.Should().NotBeNull();
            entity.Id.Should().Be(expectedIdentity.CanonicalId);
            entity.SeasonWeekId.Should().Be(seasonWeekId);
            entity.PollName.Should().Be("AFCA Coaches Poll");
            entity.PollShortName.Should().Be("AFCA Coaches Poll");
            entity.PollType.Should().Be("usa");
            entity.Headline.Should().Be("2025 NCAA Football Rankings - AFCA Coaches Poll Preseason");
            entity.ShortHeadline.Should().Be("2025 AFCA Coaches Poll: Preseason");

            // occurrence
            entity.OccurrenceNumber.Should().Be(1);
            entity.OccurrenceType.Should().Be("week");
            entity.OccurrenceIsLast.Should().BeFalse();
            entity.OccurrenceValue.Should().Be("1");
            entity.OccurrenceDisplay.Should().Be("Preseason");

            // dates converted to UTC DateTime in the entity
            entity.DateUtc.Should().Be(DateTime.Parse("2025-08-04T07:00Z").ToUniversalTime());
            entity.LastUpdatedUtc.Should().Be(DateTime.Parse("2025-08-04T19:24Z").ToUniversalTime());

            // audit fields
            entity.CreatedBy.Should().Be(correlationId);
            entity.CreatedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

            // external ids (from $ref)
            entity.ExternalIds.Should().ContainSingle();
            var ext = entity.ExternalIds.Single();
            ext.Provider.Should().Be(SourceDataProvider.Espn);
            ext.SourceUrl.Should().Be(expectedIdentity.CleanUrl);
            ext.Value.Should().Be(expectedIdentity.UrlHash);
            ext.SourceUrlHash.Should().Be(expectedIdentity.UrlHash);

            // counts
            entity.Entries.Should().HaveCount(51); // 25 Ranked; 26 Others receiving votes

            // first rank entry spot-checks
            var first = entity.Entries.First();
            first.Current.Should().Be(1);
            first.Previous.Should().Be(0);
            first.Points.Should().Be(1606m);
            first.FirstPlaceVotes.Should().Be(28);
            first.Trend.Should().Be("-");
            first.RowDateUtc.Should().Be(DateTime.Parse("2025-08-04T07:00Z").ToUniversalTime());
            first.RowLastUpdatedUtc.Should().Be(DateTime.Parse("2025-08-04T19:24Z").ToUniversalTime());

            // record + stats
            first.RecordSummary.Should().Be("0-0");
            first.Stats.Should().HaveCount(2);

            var wins = first.Stats.Single(s => s.Name == "wins");
            wins.DisplayName.Should().Be("Wins");
            wins.ShortDisplayName.Should().Be("W");
            wins.Description.Should().Be("Wins");
            wins.Abbreviation.Should().Be("W");
            wins.Type.Should().Be("wins");
            wins.Value.Should().Be(0m);
            wins.DisplayValue.Should().Be("0");

            var losses = first.Stats.Single(s => s.Name == "losses");
            losses.Type.Should().Be("losses");
            losses.Value.Should().Be(0m);
            losses.DisplayValue.Should().Be("0");
        }

        [Fact]
        public async Task ProcessNewSeasonTypeWeekRankings_CreatesRankingEntity()
        {
            // Arrange
            var correlationId = Guid.NewGuid();
            var seasonId = Guid.NewGuid();
            var seasonPhaseId = Guid.NewGuid();
            var seasonWeekId = Guid.NewGuid();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var season = Fixture.Build<Season>()
                .With(x => x.Id, seasonId)
                .Create();

            var seasonPhase = Fixture.Build<SeasonPhase>()
                .With(x => x.Id, seasonPhaseId)
                .With(x => x.SeasonId, seasonId)
                .With(x => x.Season, season)
                .Create();

            var weekRefUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1?lang=en&region=us";
            var weekHash = generator.Generate(weekRefUrl).UrlHash;

            var seasonWeek = Fixture.Build<SeasonWeek>()
                .With(x => x.Id, seasonWeekId)
                .With(x => x.SeasonId, seasonId)
                .With(x => x.SeasonPhaseId, seasonPhaseId)
                .With(x => x.SeasonPhase, seasonPhase)
                .With(x => x.ExternalIds, new List<SeasonWeekExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = weekRefUrl,
                        SourceUrlHash = weekHash,
                        Value = weekHash
                    }
                })
                .Create();

            await FootballDataContext.Seasons.AddAsync(season);
            await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
            await FootballDataContext.SeasonWeeks.AddAsync(seasonWeek);
            await FootballDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaSeasonTypeWeekRankings.json");
            var command = new ProcessDocumentCommand(
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                2025,
                DocumentType.SeasonTypeWeekRankings,
                json,
                correlationId,
                parentId: seasonWeekId.ToString(),
                sourceUri: new Uri(weekRefUrl),
                urlHash: weekHash
            );

            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var ranking = await FootballDataContext.SeasonRankings
                .Include(r => r.Entries)
                .ThenInclude(e => e.Stats)
                .Include(r => r.ExternalIds)
                .FirstOrDefaultAsync();

            ranking.Should().NotBeNull();
            ranking!.SeasonWeekId.Should().Be(seasonWeekId);
            ranking.Entries.Should().HaveCount(51);
            ranking.ExternalIds.Should().ContainSingle(x => x.SourceUrlHash == weekHash);

            var first = ranking.Entries.First();
            first.Current.Should().Be(1);
            first.Stats.Should().Contain(s => s.Name == "wins" && s.Value == 0m);
        }
    }
}