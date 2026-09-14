using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    /// <summary>
    /// Tests for SeasonTypeWeekRankingsDocumentProcessor.
    /// Optimized to eliminate AutoFixture overhead.
    /// </summary>
    public class SeasonTypeWeekRankingsDocumentProcessorTests
    : ProducerTestBase<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenJsonIsValid_DtoDeserializes()
        {
            // arrange
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeWeekRankings.json");

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
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeWeekRankings.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            var seasonPollId = Guid.NewGuid();

            var seasonWeekId = Guid.NewGuid();
            var generator = new ExternalRefIdentityGenerator();
            var correlationId = Guid.NewGuid();
            Dictionary<string, Guid> franchiseDictionary = new();

            foreach (var entry in dto!.Ranks)
            {
                if (entry.Team?.Ref is not null)
                {
                    var teamIdentity = generator.Generate(entry.Team.Ref);
                    franchiseDictionary.TryAdd(teamIdentity.CleanUrl, teamIdentity.CanonicalId);
                }
            }

            foreach (var other in dto!.Others)
            {
                if (other.Team?.Ref is not null)
                {
                    var teamIdentity = generator.Generate(other.Team.Ref);
                    franchiseDictionary.TryAdd(teamIdentity.CleanUrl, teamIdentity.CanonicalId);
                }
            }

            var expectedIdentity = generator.Generate(dto.Ref!);

            // act
            var before = DateTime.UtcNow;
            var entity = dto.AsEntity(seasonPollId, seasonWeekId, generator, franchiseDictionary, correlationId);
            var after = DateTime.UtcNow;

            // assert: top-level entity
            entity.Should().NotBeNull();
            entity.Id.Should().Be(expectedIdentity.CanonicalId);
            entity.SeasonWeekId.Should().Be(seasonWeekId);
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
            first.Points.Should().Be(1606);
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
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeWeekRankings.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            // Arrange
            var correlationId = Guid.NewGuid();
            var seasonId = Guid.NewGuid();
            var seasonPhaseId = Guid.NewGuid();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var seasonPollRef = EspnUriMapper.SeasonPollWeekRefToSeasonPollRef(dto!.Ref);
            var seasonPollIdentity = generator.Generate(seasonPollRef);

            // OPTIMIZATION: Direct instantiation
            var seasonPoll = new SeasonPoll
            {
                Id = seasonPollIdentity.CanonicalId,
                Name = "AP Top 25",
                ShortName = "AP Poll",
                SeasonYear = 2025,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            await FootballDataContext.SeasonPolls.AddAsync(seasonPoll);
            await FootballDataContext.SaveChangesAsync();

            // OPTIMIZATION: Direct instantiation
            var season = new Season
            {
                Id = seasonId,
                Name = "2025 NCAA Football Season",
                Year = 2025,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var seasonPhase = new SeasonPhase
            {
                Id = seasonPhaseId,
                SeasonId = seasonId,
                Name = "2025 Regular Season",
                Slug = "Regular Season",
                Abbreviation = "REG",
                TypeCode = 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var weekRefUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1/rankings/2?lang=en&region=us";
            var weekHash = generator.Generate(weekRefUrl).UrlHash;

            var seasonWeekIdentity = generator.Generate(dto.Season.Type.Week.Ref);
            
            // OPTIMIZATION: Direct instantiation
            var seasonWeek = new SeasonWeek
            {
                Id = seasonWeekIdentity.CanonicalId,
                Number = 1,
                SeasonId = seasonId,
                SeasonPhaseId = seasonPhaseId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<SeasonWeekExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        SeasonWeekId = seasonWeekIdentity.CanonicalId,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = weekRefUrl,
                        SourceUrlHash = weekHash,
                        Value = weekHash
                    }
                }
            };

            await FootballDataContext.Seasons.AddAsync(season);
            await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
            await FootballDataContext.SeasonWeeks.AddAsync(seasonWeek);
            await FootballDataContext.SaveChangesAsync();

            var command = new ProcessDocumentCommand(
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                2025,
                DocumentType.SeasonTypeWeekRankings,
                json,
                messageId: Guid.NewGuid(),
                correlationId: correlationId,
                parentId: seasonWeekIdentity.CanonicalId.ToString(),
                sourceUri: new Uri(weekRefUrl),
                urlHash: weekHash
            );

            await SeedFranchisesAndSeasonsFromDto(dto, generator);

            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var ranking = await FootballDataContext.SeasonPollWeeks
                .Include(r => r.Entries)
                .ThenInclude(e => e.Stats)
                .Include(r => r.ExternalIds)
                .FirstOrDefaultAsync();

            ranking.Should().NotBeNull();
            ranking!.SeasonWeekId.Should().Be(seasonWeekIdentity.CanonicalId);
            ranking.Entries.Should().HaveCount(51);
            ranking.ExternalIds.Should().ContainSingle(x => x.SourceUrlHash == weekHash);

            var first = ranking.Entries.First();
            first.Current.Should().Be(1);
            first.Stats.Should().Contain(s => s.Name == "wins" && s.Value == 0m);
        }

        [Fact]
        public async Task ProcessExistingSeasonTypeWeekRankings_WhenUnchanged_IsIdempotentNoOp()
        {
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeWeekRankings.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            // Arrange — same setup as ProcessNewSeasonTypeWeekRankings_CreatesRankingEntity
            var correlationId = Guid.NewGuid();
            var seasonId = Guid.NewGuid();
            var seasonPhaseId = Guid.NewGuid();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var seasonPollRef = EspnUriMapper.SeasonPollWeekRefToSeasonPollRef(dto!.Ref);
            var seasonPollIdentity = generator.Generate(seasonPollRef);

            var seasonPoll = new SeasonPoll
            {
                Id = seasonPollIdentity.CanonicalId,
                Name = "AFCA Coaches Poll",
                ShortName = "Coaches",
                SeasonYear = 2025,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            await FootballDataContext.SeasonPolls.AddAsync(seasonPoll);

            var season = new Season
            {
                Id = seasonId,
                Name = "2025 NCAA Football Season",
                Year = 2025,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var seasonPhase = new SeasonPhase
            {
                Id = seasonPhaseId,
                SeasonId = seasonId,
                Name = "2025 Regular Season",
                Slug = "Regular Season",
                Abbreviation = "REG",
                TypeCode = 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var seasonWeekIdentity = generator.Generate(dto.Season.Type.Week.Ref);
            var weekRefUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1/rankings/2?lang=en&region=us";
            var weekHash = generator.Generate(weekRefUrl).UrlHash;

            var seasonWeek = new SeasonWeek
            {
                Id = seasonWeekIdentity.CanonicalId,
                Number = 1,
                SeasonId = seasonId,
                SeasonPhaseId = seasonPhaseId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.Seasons.AddAsync(season);
            await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
            await FootballDataContext.SeasonWeeks.AddAsync(seasonWeek);
            await FootballDataContext.SaveChangesAsync();

            await SeedFranchisesAndSeasonsFromDto(dto, generator);

            var command = new ProcessDocumentCommand(
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                2025,
                DocumentType.SeasonTypeWeekRankings,
                json,
                messageId: Guid.NewGuid(),
                correlationId: correlationId,
                parentId: seasonPollIdentity.CanonicalId.ToString(),
                sourceUri: new Uri(weekRefUrl),
                urlHash: weekHash
            );

            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            // Act — process the same document twice (simulates at-least-once redelivery / backfill re-source)
            await sut.ProcessAsync(command);
            await sut.ProcessAsync(command);

            // Assert — still exactly one SeasonPollWeek with its full entry set; the second
            // pass is a no-op (no duplicate rows, no exception).
            var rankings = await FootballDataContext.SeasonPollWeeks
                .Include(r => r.Entries)
                .ToListAsync();

            rankings.Should().ContainSingle("re-delivering an unchanged document must not create a duplicate");
            rankings.Single().Entries.Should().HaveCount(51);
        }

        [Fact]
        public async Task WhenParentIdIsNotGuid_DerivesSeasonPollIdFromRef()
        {
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeWeekRankings.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            var correlationId = Guid.NewGuid();
            var seasonId = Guid.NewGuid();
            var seasonPhaseId = Guid.NewGuid();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            // Create the SeasonPoll with an ID matching what the fallback derivation produces
            var seasonPollRef = EspnUriMapper.SeasonPollWeekRefToSeasonPollRef(dto!.Ref);
            var seasonPollIdentity = generator.Generate(seasonPollRef);

            var seasonPoll = new SeasonPoll
            {
                Id = seasonPollIdentity.CanonicalId,
                Name = "AFCA Coaches Poll",
                ShortName = "Coaches",
                SeasonYear = 2025,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            await FootballDataContext.SeasonPolls.AddAsync(seasonPoll);

            // Seed Season + SeasonPhase + SeasonWeek
            var season = new Season
            {
                Id = seasonId,
                Name = "2025 NCAA Football Season",
                Year = 2025,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var seasonPhase = new SeasonPhase
            {
                Id = seasonPhaseId,
                SeasonId = seasonId,
                Name = "2025 Regular Season",
                Slug = "Regular Season",
                Abbreviation = "REG",
                TypeCode = 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var seasonWeekIdentity = generator.Generate(dto.Season.Type.Week.Ref);
            var weekRefUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1/rankings/2?lang=en&region=us";
            var weekHash = generator.Generate(weekRefUrl).UrlHash;

            var seasonWeek = new SeasonWeek
            {
                Id = seasonWeekIdentity.CanonicalId,
                Number = 1,
                SeasonId = seasonId,
                SeasonPhaseId = seasonPhaseId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.Seasons.AddAsync(season);
            await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
            await FootballDataContext.SeasonWeeks.AddAsync(seasonWeek);

            await SeedFranchisesAndSeasonsFromDto(dto, generator);

            // Pass a non-GUID parentId to trigger fallback derivation
            var command = new ProcessDocumentCommand(
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                2025,
                DocumentType.SeasonTypeWeekRankings,
                json,
                messageId: Guid.NewGuid(),
                correlationId: correlationId,
                parentId: "not-a-guid",
                sourceUri: new Uri(weekRefUrl),
                urlHash: weekHash
            );

            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert — entity should exist with the correct SeasonPollId (not Guid.Empty)
            var ranking = await FootballDataContext.SeasonPollWeeks
                .Include(r => r.Entries)
                .FirstOrDefaultAsync();

            ranking.Should().NotBeNull();
            ranking!.SeasonPollId.Should().Be(seasonPollIdentity.CanonicalId, "fallback should assign the derived SeasonPollId");
            ranking.SeasonPollId.Should().NotBe(Guid.Empty, "seasonPollId must not remain Guid.Empty after fallback");
            ranking.Entries.Should().HaveCount(51);
        }

        [Fact]
        public async Task WhenParentIdIsNotGuid_AndSeasonPollNotFound_ReturnsEarly()
        {
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeWeekRankings.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            // Seed all prerequisites EXCEPT SeasonPoll so the only failure path is the missing poll
            var seasonId = Guid.NewGuid();
            var seasonPhaseId = Guid.NewGuid();

            var season = new Season
            {
                Id = seasonId,
                Name = "2025 NCAA Football Season",
                Year = 2025,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var seasonPhase = new SeasonPhase
            {
                Id = seasonPhaseId,
                SeasonId = seasonId,
                Name = "2025 Regular Season",
                Slug = "Regular Season",
                Abbreviation = "REG",
                TypeCode = 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var seasonWeekIdentity = generator.Generate(dto!.Season.Type.Week.Ref);
            var weekRefUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks/1/rankings/2?lang=en&region=us";
            var weekHash = generator.Generate(weekRefUrl).UrlHash;

            var seasonWeek = new SeasonWeek
            {
                Id = seasonWeekIdentity.CanonicalId,
                Number = 1,
                SeasonId = seasonId,
                SeasonPhaseId = seasonPhaseId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.Seasons.AddAsync(season);
            await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
            await FootballDataContext.SeasonWeeks.AddAsync(seasonWeek);

            await SeedFranchisesAndSeasonsFromDto(dto, generator);

            // Do NOT seed a SeasonPoll — fallback derivation should fail
            var command = new ProcessDocumentCommand(
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                2025,
                DocumentType.SeasonTypeWeekRankings,
                json,
                messageId: Guid.NewGuid(),
                correlationId: Guid.NewGuid(),
                parentId: "not-a-guid",
                sourceUri: new Uri(weekRefUrl),
                urlHash: weekHash
            );

            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert — no entity should be created (specifically because SeasonPoll is missing)
            var ranking = await FootballDataContext.SeasonPollWeeks.FirstOrDefaultAsync();
            ranking.Should().BeNull("processor should return early when SeasonPoll cannot be derived");
        }

        /// <summary>
        /// The 2026 Week 3 AP poll as ESPN served it on 2026-09-13: $ref .../weeks/3,
        /// occurrence 3, FOR week 3, with season.type.week.number still 2 because
        /// ESPN's current-week pointer had not rolled yet (Monday 07:00Z). The row
        /// must land on regular-season week 3 - the slot the Week 2 poll had been
        /// misfiled into in production, which is what the 23505 collision was.
        /// </summary>
        [Fact]
        public async Task ProcessNewSeasonTypeWeekRankings_SundayFetch_FilesWeek3PollOnWeek3()
        {
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonPollWeek.2026.Wk3.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            // Fixture sanity: the poll's own week and the season's pointer disagree.
            dto!.Occurrence.Number.Should().Be(3);
            dto.Season.Type.Type.Should().Be(2);
            dto.Season.Type.Week.Number.Should().Be(2);

            var ids = await SeedWeek3ScenarioAsync(dto, seedRegularSeasonWeek3: true);
            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            await sut.ProcessAsync(BuildCommand(json, dto, ids.SeasonPollId));

            var ranking = await FootballDataContext.SeasonPollWeeks.SingleOrDefaultAsync();
            ranking.Should().NotBeNull();
            ranking!.OccurrenceNumber.Should().Be(3);
            ranking.SeasonWeekId.Should().Be(ids.RegularSeasonWeek3Id);
        }

        /// <summary>
        /// The same document fetched after ESPN's pointer rolled (Monday 07:00Z):
        /// season.type.week.number is now 3. Pointer + 1 selects week 4; the poll's
        /// own week is still 3. This is the shape where the two formulas diverge, so
        /// it is the test that pins the fetch-time-pointer bug.
        /// </summary>
        [Fact]
        public async Task ProcessNewSeasonTypeWeekRankings_MondayFetch_StillFilesWeek3PollOnWeek3()
        {
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonPollWeek.2026.Wk3.json");
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            node["season"]!["type"]!["week"]!["number"] = 3;
            json = node.ToJsonString();

            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();
            dto!.Occurrence.Number.Should().Be(3);
            dto.Season.Type.Week.Number.Should().Be(3);

            var ids = await SeedWeek3ScenarioAsync(dto, seedRegularSeasonWeek3: true);
            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            await sut.ProcessAsync(BuildCommand(json, dto, ids.SeasonPollId));

            var ranking = await FootballDataContext.SeasonPollWeeks.SingleOrDefaultAsync();
            ranking.Should().NotBeNull();
            ranking!.SeasonWeekId.Should().Be(ids.RegularSeasonWeek3Id,
                "the poll's own week does not move when the season's pointer rolls");
            ranking.SeasonWeekId.Should().NotBe(ids.RegularSeasonWeek4Id);
        }

        /// <summary>
        /// Week numbers repeat across phases. With only a PRESEASON week 3 present,
        /// the regular-season poll must not take it: the lookup misses, and the
        /// dependency request names the poll's own week (.../types/2/weeks/3) -
        /// not the season's pointer week (.../types/2/weeks/2), which could never
        /// create the row this lookup needs.
        /// </summary>
        [Fact]
        public async Task ProcessNewSeasonTypeWeekRankings_WeekMissingInPhase_RequestsThePollsOwnWeek()
        {
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonPollWeek.2026.Wk3.json");
            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            var ids = await SeedWeek3ScenarioAsync(dto!, seedRegularSeasonWeek3: false);
            var bus = Mocker.GetMock<IEventBus>();
            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            await sut.ProcessAsync(BuildCommand(json, dto!, ids.SeasonPollId));

            (await FootballDataContext.SeasonPollWeeks.AnyAsync()).Should().BeFalse(
                "a preseason week 3 is not the regular-season week 3");

            bus.Verify(
                x => x.Publish(
                    It.Is<DocumentRequested>(e =>
                        e.DocumentType == DocumentType.SeasonTypeWeek &&
                        e.Uri.ToString().EndsWith("/seasons/2026/types/2/weeks/3")),
                    It.IsAny<CancellationToken>()),
                Times.Once,
                "the dependency request must name the week the lookup needs");
        }

        /// <summary>
        /// A ranking document with a season week but no occurrence cannot be
        /// filed. The guard returns without writing a row and without a
        /// dependency request (there is nothing to source that would help), and
        /// without throwing - a null dereference here would land the job in
        /// retry forever for a document that will never change.
        /// </summary>
        [Fact]
        public async Task ProcessNewSeasonTypeWeekRankings_NoOccurrence_ReturnsWithoutRowOrDependencyRequest()
        {
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonPollWeek.2026.Wk3.json");
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            node.AsObject().Remove("occurrence");
            json = node.ToJsonString();

            var dto = json.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();
            dto!.Occurrence.Should().BeNull();
            dto.Season.Type.Week.Should().NotBeNull();

            var ids = await SeedWeek3ScenarioAsync(dto, seedRegularSeasonWeek3: true);
            var bus = Mocker.GetMock<IEventBus>();
            var sut = Mocker.CreateInstance<SeasonTypeWeekRankingsDocumentProcessor<FootballDataContext>>();

            var act = async () => await sut.ProcessAsync(BuildCommand(json, dto, ids.SeasonPollId));

            await act.Should().NotThrowAsync();
            (await FootballDataContext.SeasonPollWeeks.AnyAsync()).Should().BeFalse();
            bus.Verify(
                x => x.Publish(
                    It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.SeasonTypeWeek),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private sealed record Week3ScenarioIds(
            Guid SeasonPollId,
            Guid RegularSeasonWeek3Id,
            Guid RegularSeasonWeek4Id,
            Guid PreseasonWeek3Id);

        /// <summary>
        /// 2026 season with a Preseason phase (TypeCode 1) and a Regular Season
        /// phase (TypeCode 2); weeks: preseason 3, regular-season 4, and
        /// optionally regular-season 3. Registers the real identity generator.
        /// </summary>
        private async Task<Week3ScenarioIds> SeedWeek3ScenarioAsync(
            EspnFootballSeasonTypeWeekRankingsDto dto,
            bool seedRegularSeasonWeek3)
        {
            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var seasonPollIdentity = generator.Generate(EspnUriMapper.SeasonPollWeekRefToSeasonPollRef(dto.Ref));
            await FootballDataContext.SeasonPolls.AddAsync(new SeasonPoll
            {
                Id = seasonPollIdentity.CanonicalId,
                Name = "AP Top 25",
                ShortName = "AP Poll",
                Slug = "ap",
                SeasonYear = 2026,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            });

            var seasonId = Guid.NewGuid();
            await FootballDataContext.Seasons.AddAsync(new Season
            {
                Id = seasonId,
                Name = "2026 NCAA Football Season",
                Year = 2026,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            });

            var preseasonPhaseId = Guid.NewGuid();
            var regularSeasonPhaseId = Guid.NewGuid();
            await FootballDataContext.SeasonPhases.AddRangeAsync(
                new SeasonPhase
                {
                    Id = preseasonPhaseId,
                    SeasonId = seasonId,
                    Name = "2026 Preseason",
                    Slug = "Preseason",
                    Abbreviation = "PRE",
                    TypeCode = 1,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid()
                },
                new SeasonPhase
                {
                    Id = regularSeasonPhaseId,
                    SeasonId = seasonId,
                    Name = "2026 Regular Season",
                    Slug = "Regular Season",
                    Abbreviation = "REG",
                    TypeCode = 2,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid()
                });

            var ids = new Week3ScenarioIds(
                SeasonPollId: seasonPollIdentity.CanonicalId,
                RegularSeasonWeek3Id: Guid.NewGuid(),
                RegularSeasonWeek4Id: Guid.NewGuid(),
                PreseasonWeek3Id: Guid.NewGuid());

            SeasonWeek Week(Guid id, int number, Guid phaseId) => new()
            {
                Id = id,
                Number = number,
                SeasonId = seasonId,
                SeasonPhaseId = phaseId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.SeasonWeeks.AddAsync(Week(ids.PreseasonWeek3Id, 3, preseasonPhaseId));
            await FootballDataContext.SeasonWeeks.AddAsync(Week(ids.RegularSeasonWeek4Id, 4, regularSeasonPhaseId));
            if (seedRegularSeasonWeek3)
                await FootballDataContext.SeasonWeeks.AddAsync(Week(ids.RegularSeasonWeek3Id, 3, regularSeasonPhaseId));
            await FootballDataContext.SaveChangesAsync();

            await SeedFranchisesAndSeasonsFromDto(dto, generator, seasonYear: 2026);

            return ids;
        }

        private static ProcessDocumentCommand BuildCommand(
            string json,
            EspnFootballSeasonTypeWeekRankingsDto dto,
            Guid seasonPollId)
        {
            var docIdentity = new ExternalRefIdentityGenerator().Generate(dto.Ref);
            return new ProcessDocumentCommand(
                SourceDataProvider.Espn,
                Sport.FootballNcaa,
                2026,
                DocumentType.SeasonTypeWeekRankings,
                json,
                messageId: Guid.NewGuid(),
                correlationId: Guid.NewGuid(),
                parentId: seasonPollId.ToString(),
                sourceUri: dto.Ref,
                urlHash: docIdentity.UrlHash);
        }

        private async Task SeedFranchisesAndSeasonsFromDto(
            EspnFootballSeasonTypeWeekRankingsDto dto,
            ExternalRefIdentityGenerator generator,
            int seasonYear = 2025)
        {
            var allTeams = dto.Ranks.Concat<dynamic>(dto.Others).ToList();

            foreach (var entry in allTeams)
            {
                var teamIdentity = generator.Generate(entry.Team.Ref);

                var franchise = new Franchise
                {
                    Id = teamIdentity.CanonicalId,
                    ColorCodeHex = "#FFFFFF",
                    DisplayName = "Team",
                    DisplayNameShort = "TM",
                    Location = "City",
                    Name = "Team",
                    Slug = "team",
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid(),
                    ExternalIds = new List<FranchiseExternalId>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            FranchiseId = teamIdentity.CanonicalId,
                            Provider = SourceDataProvider.Espn,
                            SourceUrl = teamIdentity.CleanUrl,
                            SourceUrlHash = teamIdentity.UrlHash,
                            Value = teamIdentity.UrlHash
                        }
                    }
                };
                await FootballDataContext.Franchises.AddAsync(franchise);

                var franchiseSeason = new FranchiseSeason
                {
                    Id = teamIdentity.CanonicalId,
                    ColorCodeHex = "#FFFFFF",
                    DisplayName = "Team",
                    DisplayNameShort = "TM",
                    Location = "City",
                    Name = "Team",
                    Slug = "team",
                    FranchiseId = franchise.Id,
                    SeasonYear = seasonYear,
                    Abbreviation = "TM",
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid(),
                    ExternalIds = new List<FranchiseSeasonExternalId>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            FranchiseSeasonId = teamIdentity.CanonicalId,
                            Provider = SourceDataProvider.Espn,
                            SourceUrl = teamIdentity.CleanUrl,
                            SourceUrlHash = teamIdentity.UrlHash,
                            Value = teamIdentity.UrlHash
                        }
                    }
                };
                await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            }

            await FootballDataContext.SaveChangesAsync();
        }
    }
}
