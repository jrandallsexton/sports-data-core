using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;
using Moq.AutoMock;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;
using SportsData.Producer.Tests.Unit.Infrastructure;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

[Collection("Sequential")]
public class TeamSeasonLeadersDocumentProcessorTests : ProducerTestBase<TeamSeasonLeadersDocumentProcessor<TeamSportDataContext>>
{
    private EspnLeadersDto _dto;

    public TeamSeasonLeadersDocumentProcessorTests()
    {
        var documentJson = LoadJsonTestData("EspnFootballNcaaTeamSeasonLeaders.json").Result;
        _dto = documentJson.FromJson<EspnLeadersDto>()!;
    }

    private async Task SeedTestDataAsync(EspnLeadersDto leadersDto, ExternalRefIdentityGenerator identityGenerator, Guid franchiseSeasonId, bool seedCategories = true)
    {
        // Seed franchise season
        var teamSeasonRef = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/99");
        var teamSeasonIdentity = identityGenerator.Generate(teamSeasonRef);

        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            Slug = "lsu-tigers-2025",
            Location = "LSU",
            Name = "Tigers",
            Abbreviation = "LSU",
            DisplayName = "LSU Tigers",
            DisplayNameShort = "LSU",
            ColorCodeHex = "#461D7C",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = franchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = teamSeasonIdentity.CleanUrl,
                    SourceUrlHash = teamSeasonIdentity.UrlHash,
                    Value = teamSeasonIdentity.UrlHash
                }
            ]
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Seed LeaderCategories (optional)
        if (seedCategories)
        {
            var nextCategoryId = 1;
            foreach (var category in leadersDto.Categories)
            {
                FootballDataContext.LeaderCategories.Add(new CompetitionLeaderCategory
                {
                    Id = nextCategoryId++,
                    Name = category.Name,
                    DisplayName = category.DisplayName,
                    ShortDisplayName = category.ShortDisplayName,
                    Abbreviation = category.Abbreviation,
                    CreatedUtc = DateTime.UtcNow
                });
            }
        }

        // Seed athlete seasons for all leaders in the DTO
        var athleteSeasonIds = new HashSet<Guid>();
        foreach (var category in leadersDto.Categories)
        {
            foreach (var leader in category.Leaders)
            {
                var athleteSeasonIdentity = identityGenerator.Generate(leader.Athlete.Ref);
                if (athleteSeasonIds.Contains(athleteSeasonIdentity.CanonicalId))
                    continue;

                var athleteId = Guid.NewGuid();
                var athleteSeasonId = athleteSeasonIdentity.CanonicalId;
                athleteSeasonIds.Add(athleteSeasonId);

                var athleteSeason = new FootballAthleteSeason
                {
                    Id = athleteSeasonId,
                    AthleteId = athleteId,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid(),
                    ExternalIds =
                    [
                        new AthleteSeasonExternalId
                        {
                            Id = Guid.NewGuid(),
                            AthleteSeasonId = athleteSeasonId,
                            Provider = SourceDataProvider.Espn,
                            SourceUrl = athleteSeasonIdentity.CleanUrl,
                            SourceUrlHash = athleteSeasonIdentity.UrlHash,
                            Value = athleteSeasonIdentity.UrlHash
                        }
                    ]
                };

                await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
            }
        }

        await FootballDataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_CreatesLeaders_WhenValidData()
    {
        // Arrange
        var documentJson = await LoadJsonTestData("EspnFootballNcaaTeamSeasonLeaders.json");
        var dto = documentJson.FromJson<EspnLeadersDto>();

        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();

        await SeedTestDataAsync(dto!, identityGenerator, franchiseSeasonId);

        var leadersIdentity = identityGenerator.Generate(dto.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.UrlHash, leadersIdentity.UrlHash)
            .With(x => x.DocumentType, DocumentType.TeamSeasonLeaders)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var config = new DocumentProcessingConfig { EnableDependencyRequests = false };
        Mocker.Use(config);

        var sut = Mocker.CreateInstance<TeamSeasonLeadersDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var leaderCount = await FootballDataContext.FranchiseSeasonLeaders.CountAsync();
        leaderCount.Should().BeGreaterThan(0);

        var statCount = await FootballDataContext.FranchiseSeasonLeaderStats.CountAsync();
        statCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProcessAsync_ReplacesExistingLeaders_WhenLeadersAlreadyExist()
    {
        // Arrange
        var documentJson = await LoadJsonTestData("EspnFootballNcaaTeamSeasonLeaders.json");
        var leadersDto = documentJson.FromJson<EspnLeadersDto>();

        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();

        await SeedTestDataAsync(leadersDto!, identityGenerator, franchiseSeasonId);

        var leadersIdentity = identityGenerator.Generate(leadersDto.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.UrlHash, leadersIdentity.UrlHash)
            .With(x => x.DocumentType, DocumentType.TeamSeasonLeaders)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var config = new DocumentProcessingConfig { EnableDependencyRequests = false };
        Mocker.Use(config);

        var sut = Mocker.CreateInstance<TeamSeasonLeadersDocumentProcessor<FootballDataContext>>();

        // Process first time
        await sut.ProcessAsync(command);

        var firstPassLeaderCount = await FootballDataContext.FranchiseSeasonLeaders.CountAsync();
        var firstPassStatCount = await FootballDataContext.FranchiseSeasonLeaderStats.CountAsync();

        // Act - Process again (wholesale replacement)
        await sut.ProcessAsync(command);

        // Assert - counts should remain the same (replacement, not additive)
        var secondPassLeaderCount = await FootballDataContext.FranchiseSeasonLeaders.CountAsync();
        var secondPassStatCount = await FootballDataContext.FranchiseSeasonLeaderStats.CountAsync();

        secondPassLeaderCount.Should().Be(firstPassLeaderCount);
        secondPassStatCount.Should().Be(firstPassStatCount);
    }

    [Fact]
    public async Task ProcessAsync_CreatesCategory_WhenLeaderCategoryNotFound()
    {
        // Arrange
        var documentJson = await LoadJsonTestData("EspnFootballNcaaTeamSeasonLeaders.json");
        var dto = documentJson.FromJson<EspnLeadersDto>();

        // Modify DTO to include unknown category
        dto!.Categories.Add(new EspnLeadersCategoryDto
        {
            Name = "unknownCategory",
            DisplayName = "Unknown Category",
            ShortDisplayName = "UNK",
            Abbreviation = "UNK",
            Leaders = []
        });

        var modifiedJson = dto.ToJson();

        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();

        // Don't seed categories - let the processor create them
        await SeedTestDataAsync(dto, identityGenerator, franchiseSeasonId, seedCategories: false);

        var leadersIdentity = identityGenerator.Generate(dto.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, modifiedJson)
            .With(x => x.UrlHash, leadersIdentity.UrlHash)
            .With(x => x.DocumentType, DocumentType.TeamSeasonLeaders)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var config = new DocumentProcessingConfig { EnableDependencyRequests = false };
        Mocker.Use(config);

        var sut = Mocker.CreateInstance<TeamSeasonLeadersDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - should create all categories including the unknown one
        var categoryCount = await FootballDataContext.LeaderCategories.CountAsync();
        categoryCount.Should().Be(dto.Categories.Count);

        var unknownCategory = await FootballDataContext.LeaderCategories
            .FirstOrDefaultAsync(x => x.Name == "unknownCategory");
        unknownCategory.Should().NotBeNull();
        unknownCategory!.DisplayName.Should().Be("Unknown Category");

        var leaderCount = await FootballDataContext.FranchiseSeasonLeaders.CountAsync();
        leaderCount.Should().Be(dto.Categories.Count); // All categories should have leaders created
    }

    [Fact]
    public async Task ProcessAsync_BatchPublishesMissingDependencies_WhenAthleteSeasonsMissing()
    {
        // Arrange - Create minimal inline test data with exactly 2 unique athletes
        var dto = new EspnLeadersDto
        {
            Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/teams/99/leaders"),
            Categories = new List<EspnLeadersCategoryDto>
            {
                new()
                {
                    Name = "passingLeader",
                    DisplayName = "Passing Leader",
                    ShortDisplayName = "PASS",
                    Abbreviation = "PYDS",
                    Leaders = new List<EspnLeadersLeaderDto>
                    {
                        new()
                        {
                            DisplayValue = "100 YDS",
                            Value = 100,
                            Athlete = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/1001") },
                            Statistics = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/teams/99/athletes/1001/statistics/0") }
                        },
                        new()
                        {
                            DisplayValue = "200 YDS",
                            Value = 200,
                            Athlete = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/1002") },
                            Statistics = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/teams/99/athletes/1002/statistics/0") }
                        }
                    }
                }
            }
        };

        var documentJson = dto.ToJson();

        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();

        // Seed only franchise season, NOT athlete seasons - this will trigger dependency requests
        var teamSeasonRef = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/99");
        var teamSeasonIdentity = identityGenerator.Generate(teamSeasonRef);

        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            Slug = "lsu-tigers-2025",
            Location = "LSU",
            Name = "Tigers",
            Abbreviation = "LSU",
            DisplayName = "LSU Tigers",
            DisplayNameShort = "LSU",
            ColorCodeHex = "#461D7C",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = franchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = teamSeasonIdentity.CleanUrl,
                    SourceUrlHash = teamSeasonIdentity.UrlHash,
                    Value = teamSeasonIdentity.UrlHash
                }
            ]
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Seed LeaderCategories to avoid concurrent creation issues
        var nextCategoryId = 1;
        foreach (var category in dto!.Categories.Where(c => c != null))
        {
            FootballDataContext.LeaderCategories.Add(new CompetitionLeaderCategory
            {
                Id = nextCategoryId++,
                Name = category.Name,
                DisplayName = category.DisplayName,
                ShortDisplayName = category.ShortDisplayName,
                Abbreviation = category.Abbreviation,
                CreatedUtc = DateTime.UtcNow
            });
        }

        await FootballDataContext.SaveChangesAsync();

        var leadersIdentity = identityGenerator.Generate(dto.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.UrlHash, leadersIdentity.UrlHash)
            .With(x => x.DocumentType, DocumentType.TeamSeasonLeaders)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.AttemptCount, 0)
            .Create();

        var busMock = Mocker.GetMock<IEventBus>();
        var config = new DocumentProcessingConfig { EnableDependencyRequests = true };
        Mocker.Use(config);

        var sut = Mocker.CreateInstance<TeamSeasonLeadersDocumentProcessor<FootballDataContext>>();

        // We have exactly 2 unique athletes in our test data
        var uniqueAthleteSeasons = 2;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ExternalDocumentNotSourcedException>(
            () => sut.ProcessAsync(command));

        exception.Message.Should().Contain($"Missing {uniqueAthleteSeasons} AthleteSeason document(s)");

        // Verify batch publishing - should publish once per unique athlete season
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.AthleteSeason),
            It.IsAny<CancellationToken>()), Times.Exactly(uniqueAthleteSeasons));

        // Verify no leaders were created (preflight failed)
        var leaderCount = await FootballDataContext.FranchiseSeasonLeaders.CountAsync();
        leaderCount.Should().Be(0);
    }

    [Fact]
    public async Task ProcessAsync_DeduplicatesDependencies_WhenSameAthleteAppearsMultipleTimes()
    {
        // Arrange - Create test data where same athlete appears twice (should only publish once)
        var dto = new EspnLeadersDto
        {
            Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/teams/99/leaders"),
            Categories = new List<EspnLeadersCategoryDto>
            {
                new()
                {
                    Name = "passingLeader",
                    DisplayName = "Passing Leader",
                    ShortDisplayName = "PASS",
                    Abbreviation = "PYDS",
                    Leaders = new List<EspnLeadersLeaderDto>
                    {
                        new()
                        {
                            DisplayValue = "100 YDS",
                            Value = 100,
                            Athlete = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/1001") },
                            Statistics = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/teams/99/athletes/1001/statistics/0") }
                        },
                        new()
                        {
                            DisplayValue = "200 YDS",
                            Value = 200,
                            Athlete = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/1001") }, // SAME athlete
                            Statistics = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/teams/99/athletes/1001/statistics/0") }
                        }
                    }
                }
            }
        };

        var modifiedJson = dto.ToJson();

        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();

        // Seed only franchise season, NOT athlete seasons
        var teamSeasonRef = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/99");
        var teamSeasonIdentity = identityGenerator.Generate(teamSeasonRef);

        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            Slug = "lsu-tigers-2025",
            Location = "LSU",
            Name = "Tigers",
            Abbreviation = "LSU",
            DisplayName = "LSU Tigers",
            DisplayNameShort = "LSU",
            ColorCodeHex = "#461D7C",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = franchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = teamSeasonIdentity.CleanUrl,
                    SourceUrlHash = teamSeasonIdentity.UrlHash,
                    Value = teamSeasonIdentity.UrlHash
                }
            ]
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Seed LeaderCategories to avoid concurrent creation issues
        var nextCategoryId = 1;
        foreach (var category in dto.Categories.Where(c => c != null))
        {
            FootballDataContext.LeaderCategories.Add(new CompetitionLeaderCategory
            {
                Id = nextCategoryId++,
                Name = category.Name,
                DisplayName = category.DisplayName,
                ShortDisplayName = category.ShortDisplayName,
                Abbreviation = category.Abbreviation,
                CreatedUtc = DateTime.UtcNow
            });
        }

        await FootballDataContext.SaveChangesAsync();

        var leadersIdentity = identityGenerator.Generate(dto.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, modifiedJson)
            .With(x => x.UrlHash, leadersIdentity.UrlHash)
            .With(x => x.DocumentType, DocumentType.TeamSeasonLeaders)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.AttemptCount, 0)
            .Create();

        var busMock = Mocker.GetMock<IEventBus>();
        var config = new DocumentProcessingConfig { EnableDependencyRequests = true };
        Mocker.Use(config);

        var sut = Mocker.CreateInstance<TeamSeasonLeadersDocumentProcessor<FootballDataContext>>();

        // Despite 2 leaders, we have only 1 unique athlete (deduplication test)
        var uniqueAthleteSeasons = 1;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ExternalDocumentNotSourcedException>(
            () => sut.ProcessAsync(command));

        exception.Message.Should().Contain($"Missing {uniqueAthleteSeasons} AthleteSeason document(s)");

        // Assert - should only publish once for the unique athlete (deduplication via HashSet)
        busMock.Verify(x => x.Publish(
            It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.AthleteSeason),
            It.IsAny<CancellationToken>()), Times.Exactly(uniqueAthleteSeasons));
    }
}


