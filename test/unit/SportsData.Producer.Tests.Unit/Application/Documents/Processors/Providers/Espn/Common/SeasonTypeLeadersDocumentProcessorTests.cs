using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// Fixture: EspnFootballNcaaSeasonTypeLeaders.json — trimmed from the REAL
/// 2025 types/3 payload: 2 categories (passingYards, rushingYards) × 3
/// leaders, season-scoped athlete/team refs intact so identity hashing
/// resolves exactly as it does in production.
/// </summary>
[Collection("Sequential")]
public class SeasonTypeLeadersDocumentProcessorTests
    : ProducerTestBase<SeasonTypeLeadersDocumentProcessor<FootballDataContext>>
{
    private static readonly DateTime FixedTestNow = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Seed an AthleteSeason + FranchiseSeason per leader ref pair; returns athlete map.</summary>
    private async Task<Dictionary<Uri, Guid>> SeedFromFixtureAsync(
        ExternalRefIdentityGenerator generator,
        EspnSeasonTypeLeadersDto dto,
        int skipAthletes = 0)
    {
        var athleteSeasonIdByRef = new Dictionary<Uri, Guid>();
        var seededTeams = new HashSet<string>();
        var leaders = dto.Categories.SelectMany(c => c.Leaders).ToList();

        foreach (var leader in leaders.Skip(skipAthletes))
        {
            if (!athleteSeasonIdByRef.ContainsKey(leader.Athlete.Ref!))
            {
                var identity = generator.Generate(leader.Athlete.Ref!);
                var athleteSeasonId = Guid.NewGuid();
                await FootballDataContext.AthleteSeasonExternalIds.AddAsync(new AthleteSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    AthleteSeasonId = athleteSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash,
                    Value = identity.UrlHash
                });
                athleteSeasonIdByRef[leader.Athlete.Ref!] = athleteSeasonId;
            }
        }

        foreach (var leader in leaders)
        {
            var teamIdentity = generator.Generate(leader.Team.Ref!);
            if (!seededTeams.Add(teamIdentity.UrlHash))
                continue;

            await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SourceUrl = teamIdentity.CleanUrl,
                SourceUrlHash = teamIdentity.UrlHash,
                Value = teamIdentity.UrlHash
            });
        }

        await FootballDataContext.SaveChangesAsync();
        return athleteSeasonIdByRef;
    }

    private ProcessDocumentCommand BuildCommand(string json) =>
        Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.SeasonTypeLeaders)
            .With(x => x.SeasonYear, 2025)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .OmitAutoProperties()
            .Create();

    [Fact]
    public async Task ProcessAsync_CreatesLeaders_WithSeasonAndTypeFromTheRef()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<SeasonTypeLeadersDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeLeaders.json");
        var dto = json.FromJson<EspnSeasonTypeLeadersDto>();
        var athleteMap = await SeedFromFixtureAsync(generator, dto!);

        // act
        await sut.ProcessAsync(BuildCommand(json));

        // assert — 2 categories × 3 leaders, season/type parsed from the ref
        var rows = await FootballDataContext.SeasonTypeLeaders.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(6);
        rows.Should().OnlyContain(r => r.SeasonYear == 2025 && r.SeasonTypeCode == 3);
        rows.Select(r => r.CategoryName).Distinct().Should().BeEquivalentTo("passingYards", "rushingYards");
        rows.Where(r => r.CategoryName == "passingYards").Select(r => r.Rank)
            .Should().BeEquivalentTo([1, 2, 3]);

        // the #1 passer maps to the seeded athlete and carries the real value
        var topPasserDto = dto!.Categories.First(c => c.Name == "passingYards").Leaders[0];
        var topPasserRow = rows.Single(r => r.CategoryName == "passingYards" && r.Rank == 1);
        topPasserRow.AthleteSeasonId.Should().Be(athleteMap[topPasserDto.Athlete.Ref!]);
        topPasserRow.Value.Should().Be(topPasserDto.Value);
        topPasserRow.FranchiseSeasonId.Should().NotBeNull("the team ref was seeded and must resolve");
    }

    [Fact]
    public async Task ProcessAsync_ReplacesExistingLeaderboard_ForSameSeasonAndType()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<SeasonTypeLeadersDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeLeaders.json");
        var dto = json.FromJson<EspnSeasonTypeLeadersDto>();
        await SeedFromFixtureAsync(generator, dto!);

        // A stale row for the SAME (season, type) — must be replaced — and
        // one for a DIFFERENT type — must survive.
        var staleSameScope = new SeasonTypeLeader
        {
            Id = Guid.NewGuid(),
            SeasonYear = 2025,
            SeasonTypeCode = 3,
            CategoryName = "stale",
            Rank = 1,
            Value = 1m,
            AthleteSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedTestNow,
            CreatedBy = Guid.NewGuid()
        };
        var otherType = new SeasonTypeLeader
        {
            Id = Guid.NewGuid(),
            SeasonYear = 2025,
            SeasonTypeCode = 2,
            CategoryName = "regularSeasonRow",
            Rank = 1,
            Value = 1m,
            AthleteSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedTestNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.SeasonTypeLeaders.AddRangeAsync(staleSameScope, otherType);
        await FootballDataContext.SaveChangesAsync();

        // act
        await sut.ProcessAsync(BuildCommand(json));

        // assert — the types/3 board replaced wholesale; the types/2 row intact
        var rows = await FootballDataContext.SeasonTypeLeaders.AsNoTracking().ToListAsync();
        rows.Where(r => r.SeasonTypeCode == 3).Should().HaveCount(6)
            .And.NotContain(r => r.CategoryName == "stale");
        rows.Where(r => r.SeasonTypeCode == 2).Should().ContainSingle(
            r => r.CategoryName == "regularSeasonRow",
            "types/2 and types/3 are distinct leaderboards; replacing one must not touch the other");
    }

    [Fact]
    public async Task ProcessAsync_SkipsUnresolvableAthletes_WithoutFailingTheDocument()
    {
        // arrange — seed all but the FIRST leader's athlete (top passer).
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<SeasonTypeLeadersDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaSeasonTypeLeaders.json");
        var dto = json.FromJson<EspnSeasonTypeLeadersDto>();
        await SeedFromFixtureAsync(generator, dto!, skipAthletes: 1);

        // act
        await sut.ProcessAsync(BuildCommand(json));

        // assert — 5 of 6 written; the unresolved #1 passer's SLOT is absent
        // (rank preserved from the document, not renumbered).
        var rows = await FootballDataContext.SeasonTypeLeaders.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(5);
        rows.Where(r => r.CategoryName == "passingYards").Select(r => r.Rank)
            .Should().BeEquivalentTo([2, 3]);
    }
}
