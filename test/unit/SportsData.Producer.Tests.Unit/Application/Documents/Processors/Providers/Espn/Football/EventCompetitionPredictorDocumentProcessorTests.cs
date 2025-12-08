using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// Tests for EventCompetitionPredictionDocumentProcessor.
/// Optimized to eliminate AutoFixture overhead.
/// </summary>
[Collection("Sequential")]
public class EventCompetitionPredictionDocumentProcessorTests :
    ProducerTestBase<EventCompetitionPredictionDocumentProcessor<FootballDataContext>>
{
    private readonly string _documentPath = "EspnFootballNcaaEventCompetitionPredictor.json";

    private async Task<ProcessDocumentCommand> SeedRequiredEntitiesAsync(Guid competitionId)
    {
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var homeRef = identityGenerator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en");
        var awayRef = identityGenerator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30?lang=en");

        // OPTIMIZATION: Direct instantiation
        var homeSeasonId = Guid.NewGuid();
        var homeSeason = new FranchiseSeason
        {
            Id = homeSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "HOME",
            DisplayName = "Home Team",
            DisplayNameShort = "HT",
            Location = "Home",
            Name = "Home Team",
            Slug = "home-team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var awaySeasonId = Guid.NewGuid();
        var awaySeason = new FranchiseSeason
        {
            Id = awaySeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "AWAY",
            DisplayName = "Away Team",
            DisplayNameShort = "AT",
            Location = "Away",
            Name = "Away Team",
            Slug = "away-team",
            ColorCodeHex = "#000000",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.FranchiseSeasons.AddRangeAsync(homeSeason, awaySeason);
        await FootballDataContext.FranchiseSeasonExternalIds.AddRangeAsync(
            new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = homeSeason.Id,
                Provider = SourceDataProvider.Espn,
                SourceUrl = homeRef.CleanUrl,
                SourceUrlHash = homeRef.UrlHash,
                Value = homeRef.UrlHash
            },
            new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = awaySeason.Id,
                Provider = SourceDataProvider.Espn,
                SourceUrl = awayRef.CleanUrl,
                SourceUrlHash = awayRef.UrlHash,
                Value = awayRef.UrlHash
            });

        // OPTIMIZATION: Direct instantiation
        var competition = new Competition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        var documentJson = await LoadJsonTestData(_documentPath);

        return Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPrediction)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.ParentId, competitionId.ToString())
            .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/predictor?lang=en".UrlHash())
            .Create();
    }

    [Fact]
    public async Task Should_CreatePredictionsAndValues()
    {
        var competitionId = Guid.NewGuid();
        var command = await SeedRequiredEntitiesAsync(competitionId);

        var sut = Mocker.CreateInstance<EventCompetitionPredictionDocumentProcessor<FootballDataContext>>();
        await sut.ProcessAsync(command);

        var predictions = await FootballDataContext.CompetitionPredictions
            .Where(x => x.CompetitionId == competitionId)
            .ToListAsync();

        predictions.Should().HaveCount(2); // home + away
        predictions.Should().Contain(x => x.IsHome);
        predictions.Should().Contain(x => !x.IsHome);

        var values = await FootballDataContext.CompetitionPredictionValues.ToListAsync();
        values.Should().NotBeEmpty();
        values.Should().AllSatisfy(v =>
        {
            v.PredictionMetricId.Should().NotBeEmpty();
            v.DisplayValue.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task Should_AddNewPredictionMetricsIfMissing()
    {
        var competitionId = Guid.NewGuid();
        var command = await SeedRequiredEntitiesAsync(competitionId);

        // Ensure PredictionMetrics table starts empty
        FootballDataContext.PredictionMetrics.RemoveRange(FootballDataContext.PredictionMetrics);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionPredictionDocumentProcessor<FootballDataContext>>();
        await sut.ProcessAsync(command);

        var metricCount = await FootballDataContext.PredictionMetrics.CountAsync();
        metricCount.Should().BeGreaterThan(10); // ~17 per team

        var sample = await FootballDataContext.PredictionMetrics
            .FirstOrDefaultAsync(x => x.Name == "gameProjection");

        sample.Should().NotBeNull();
        sample!.DisplayName.Should().Be("WIN PROB");
    }

    [Fact]
    public async Task Should_UseExistingPredictionMetricsIfAlreadySeeded()
    {
        var preexistingMetric = new PredictionMetric
        {
            Id = Guid.NewGuid(),
            Name = "gameProjection",
            DisplayName = "WIN PROB",
            ShortDisplayName = "GP",
            Abbreviation = "GP",
            Description = "Pre-seeded"
        };

        await FootballDataContext.PredictionMetrics.AddAsync(preexistingMetric);
        await FootballDataContext.SaveChangesAsync();

        var competitionId = Guid.NewGuid();
        var command = await SeedRequiredEntitiesAsync(competitionId);

        var sut = Mocker.CreateInstance<EventCompetitionPredictionDocumentProcessor<FootballDataContext>>();
        await sut.ProcessAsync(command);

        var values = await FootballDataContext.CompetitionPredictionValues
            .Where(x => x.PredictionMetricId == preexistingMetric.Id)
            .ToListAsync();

        values.Should().NotBeEmpty("should reuse pre-seeded metric by name match");
    }
}
