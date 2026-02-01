using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class EventCompetitionCompetitorRosterDocumentProcessorTests
    : ProducerTestBase<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task WhenJsonIsValid_DtoDeserializes()
    {
        // arrange
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");

        // act
        var dto = json.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        // assert
        dto.Should().NotBeNull();
        dto!.Ref.Should().NotBeNull();
        dto.Entries.Should().HaveCount(111);
        dto.Entries.Count(e => e.Statistics?.Ref != null).Should().Be(39);
        dto.Competition.Should().NotBeNull();
        dto.Team.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenProcessingRoster_PublishesChildDocumentRequestsForAthleteStatistics()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");
        var dto = json.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        // Create Competition in database (required for FK)
        var competitionIdentity = generator.Generate(dto!.Competition!.Ref!);
        var competition = new Competition
        {
            Id = competitionIdentity.CanonicalId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);

        // Create CompetitionCompetitor in database (required for FK)
        var competitorId = Guid.NewGuid();
        var competitor = new CompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competition.Id,
            HomeAway = "home",
            FranchiseSeasonId = Guid.NewGuid(),
            Order = 1,
            Winner = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);

        // Create AthleteSeason entries for all athletes with statistics (39)
        var entriesWithStats = dto.Entries.Where(e => e.Statistics?.Ref != null).ToList();
        foreach (var entry in entriesWithStats)
        {
            if (entry.Athlete?.Ref is null) continue;

            var athleteSeasonIdentity = generator.Generate(entry.Athlete.Ref);
            var athleteSeason = new FootballAthleteSeason
            {
                Id = athleteSeasonIdentity.CanonicalId,
                AthleteId = Guid.NewGuid(),
                FranchiseSeasonId = Guid.NewGuid(),
                PositionId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        }
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorRoster)
            .With(x => x.Document, json)
            .With(x => x.ParentId, competitorId.ToString())
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert - processor should complete successfully and publish 39 DocumentRequested events
        // (PublishChildDocumentRequest publishes DocumentRequested, not DocumentCreated)
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionAthleteStatistics),
            It.IsAny<CancellationToken>()), 
            Times.Exactly(39));
    }

    [Fact]
    public async Task WhenProcessingRoster_PersistsAthleteCompetitionEntries()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");
        var dto = json.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        // Create Competition in database (required for FK)
        var competitionIdentity = generator.Generate(dto!.Competition!.Ref!);
        var competition = new Competition
        {
            Id = competitionIdentity.CanonicalId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);

        // Create CompetitionCompetitor in database (required for FK)
        var competitorId = Guid.NewGuid();
        var competitor = new CompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competition.Id,
            HomeAway = "home",
            FranchiseSeasonId = Guid.NewGuid(),
            Order = 1,
            Winner = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);

        // Create AthleteSeason entries for the first 5 roster entries
        var athleteSeasons = new List<FootballAthleteSeason>();
        foreach (var entry in dto.Entries.Take(5))
        {
            if (entry.Athlete?.Ref is null) continue;

            var athleteSeasonIdentity = generator.Generate(entry.Athlete.Ref);
            var athleteSeason = new FootballAthleteSeason
            {
                Id = athleteSeasonIdentity.CanonicalId,
                AthleteId = Guid.NewGuid(),
                FranchiseSeasonId = Guid.NewGuid(),
                PositionId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            athleteSeasons.Add(athleteSeason);
        }
        await FootballDataContext.AthleteSeasons.AddRangeAsync(athleteSeasons);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorRoster)
            .With(x => x.Document, json)
            .With(x => x.ParentId, competitorId.ToString())
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert - roster entries should be persisted (at least as many as AthleteSeason entries we created)
        var rosterEntries = await FootballDataContext.AthleteCompetitions
            .Where(x => x.CompetitionId == competition.Id && x.CompetitionCompetitorId == competitorId)
            .ToListAsync();

        rosterEntries.Should().NotBeEmpty();
        rosterEntries.Should().HaveCountGreaterThanOrEqualTo(athleteSeasons.Count);

        // Verify properties are mapped correctly
        var firstEntry = rosterEntries.First();
        firstEntry.CompetitionId.Should().Be(competition.Id);
        firstEntry.CompetitionCompetitorId.Should().Be(competitorId);
        athleteSeasons.Select(a => a.Id).Should().Contain(firstEntry.AthleteSeasonId);
    }

    [Fact]
    public async Task WhenProcessingRosterTwice_ReplacesExistingEntries()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");
        var dto = json.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        // Create Competition in database
        var competitionIdentity = generator.Generate(dto!.Competition!.Ref!);
        var competition = new Competition
        {
            Id = competitionIdentity.CanonicalId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);

        // Create CompetitionCompetitor in database (required for FK)
        var competitorId = Guid.NewGuid();
        var competitor = new CompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competition.Id,
            HomeAway = "home",
            FranchiseSeasonId = Guid.NewGuid(),
            Order = 1,
            Winner = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);

        // Create AthleteSeason entries
        var athleteSeasons = new List<FootballAthleteSeason>();
        foreach (var entry in dto.Entries.Take(5))
        {
            if (entry.Athlete?.Ref is null) continue;

            var athleteSeasonIdentity = generator.Generate(entry.Athlete.Ref);
            var athleteSeason = new FootballAthleteSeason
            {
                Id = athleteSeasonIdentity.CanonicalId,
                AthleteId = Guid.NewGuid(),
                FranchiseSeasonId = Guid.NewGuid(),
                PositionId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            athleteSeasons.Add(athleteSeason);
        }
        await FootballDataContext.AthleteSeasons.AddRangeAsync(athleteSeasons);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorRoster)
            .With(x => x.Document, json)
            .With(x => x.ParentId, competitorId.ToString())
            .Create();

        // act - process first time
        await sut.ProcessAsync(command);

        var firstPassCount = await FootballDataContext.AthleteCompetitions
            .CountAsync(x => x.CompetitionId == competition.Id && x.CompetitionCompetitorId == competitorId);

        firstPassCount.Should().BeGreaterThan(0);

        // act - process second time (wholesale replacement)
        await sut.ProcessAsync(command);

        // assert - count should be the same (old entries deleted, new entries inserted)
        var secondPassCount = await FootballDataContext.AthleteCompetitions
            .CountAsync(x => x.CompetitionId == competition.Id && x.CompetitionCompetitorId == competitorId);

        secondPassCount.Should().Be(firstPassCount, "wholesale replacement should result in same count");
    }

    [Fact]
    public async Task WhenRosterEntryHasJerseyNumber_PersistsJerseyNumber()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");
        var dto = json.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        // Create Competition in database
        var competitionIdentity = generator.Generate(dto!.Competition!.Ref!);
        var competition = new Competition
        {
            Id = competitionIdentity.CanonicalId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);

        // Create CompetitionCompetitor in database (required for FK)
        var competitorId = Guid.NewGuid();
        var competitor = new CompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competition.Id,
            HomeAway = "home",
            FranchiseSeasonId = Guid.NewGuid(),
            Order = 1,
            Winner = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);

        // Find a roster entry with jersey number
        var entryWithJersey = dto.Entries.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Jersey) && e.Athlete?.Ref != null);
        entryWithJersey.Should().NotBeNull("test data should contain entries with jersey numbers");

        // Create AthleteSeason for this entry
        var athleteSeasonIdentity = generator.Generate(entryWithJersey!.Athlete!.Ref!);
        var athleteSeason = new FootballAthleteSeason
        {
            Id = athleteSeasonIdentity.CanonicalId,
            AthleteId = Guid.NewGuid(),
            FranchiseSeasonId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorRoster)
            .With(x => x.Document, json)
            .With(x => x.ParentId, competitorId.ToString())
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert - jersey number should be persisted
        var rosterEntry = await FootballDataContext.AthleteCompetitions
            .FirstOrDefaultAsync(x => x.CompetitionId == competition.Id && x.CompetitionCompetitorId == competitorId && x.AthleteSeasonId == athleteSeason.Id);

        rosterEntry.Should().NotBeNull();
        rosterEntry!.JerseyNumber.Should().Be(entryWithJersey.Jersey);
    }

    [Fact]
    public async Task WhenAthleteDidNotPlay_PersistsDidNotPlayFlag()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");
        var dto = json.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        // Create Competition in database
        var competitionIdentity = generator.Generate(dto!.Competition!.Ref!);
        var competition = new Competition
        {
            Id = competitionIdentity.CanonicalId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);

        // Create CompetitionCompetitor in database (required for FK)
        var competitorId = Guid.NewGuid();
        var competitor = new CompetitionCompetitor
        {
            Id = competitorId,
            CompetitionId = competition.Id,
            HomeAway = "home",
            FranchiseSeasonId = Guid.NewGuid(),
            Order = 1,
            Winner = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);

        // Create AthleteSeason entries for the first 5
        var athleteSeasons = new List<FootballAthleteSeason>();
        foreach (var entry in dto.Entries.Take(5))
        {
            if (entry.Athlete?.Ref is null) continue;

            var athleteSeasonIdentity = generator.Generate(entry.Athlete.Ref);
            var athleteSeason = new FootballAthleteSeason
            {
                Id = athleteSeasonIdentity.CanonicalId,
                AthleteId = Guid.NewGuid(),
                FranchiseSeasonId = Guid.NewGuid(),
                PositionId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };
            athleteSeasons.Add(athleteSeason);
        }
        await FootballDataContext.AthleteSeasons.AddRangeAsync(athleteSeasons);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorRoster)
            .With(x => x.Document, json)
            .With(x => x.ParentId, competitorId.ToString())
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert - DidNotPlay flag should be persisted correctly
        var rosterEntries = await FootballDataContext.AthleteCompetitions
            .Where(x => x.CompetitionId == competition.Id && x.CompetitionCompetitorId == competitorId)
            .ToListAsync();

        rosterEntries.Should().NotBeEmpty();
        
        // Verify DidNotPlay values match the DTO entries for the first 5 athletes we created
        var expectedDidNotPlayValues = dto.Entries.Take(5)
            .Where(e => e.Athlete?.Ref != null)
            .Select(e => e.DidNotPlay)
            .ToList();

        var actualDidNotPlayValues = rosterEntries
            .OrderBy(e => e.CreatedUtc)
            .Select(e => e.DidNotPlay)
            .ToList();

        actualDidNotPlayValues.Should().BeEquivalentTo(expectedDidNotPlayValues, 
            "the DidNotPlay flags should match the source DTO entries");
    }
}


