using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using System.Globalization;

using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using System.Text.Json;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// Tests for EventCompetitionCompetitorRecordDocumentProcessor.
///
/// Focus is on the diff-merge stat update path that replaced the prior
/// "RemoveRange + re-Add" pattern. The prior pattern issued explicit DELETE
/// statements for every stat row, which under burst-fan-out contention
/// would race sibling workers and throw DbUpdateConcurrencyException on
/// EF's row-count mismatch (the entity has no IsRowVersion token).
///
/// Diff-merge emits in-place UPDATEs for the steady-state case (ESPN ships
/// the same stat keys with new values), eliminating the race for the
/// common path. Only orphan removal / new stat additions still hit
/// DELETE / INSERT.
///
/// Stat.Id preservation is the load-bearing assertion that proves
/// diff-merge: under the prior DELETE-INSERT pattern, every SaveChanges
/// would issue a new Guid for every stat. Diff-merge keeps the same Id
/// across updates.
/// </summary>
public class EventCompetitionCompetitorRecordDocumentProcessorTests
    : ProducerTestBase<EventCompetitionCompetitorRecordDocumentProcessor<TeamSportDataContext>>
{
    private const string DocumentRef = "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401815434/competitions/401815434/competitors/28/records/61534?lang=en&region=us";

    private static string BuildDocumentJson(string type, params (string Name, double Value, string DisplayValue)[] stats)
    {
        // Numeric values rendered with InvariantCulture so the resulting
        // JSON is valid on locales with non-dot decimal separators (e.g.
        // de-DE would otherwise emit "value": 0,667 — invalid JSON).
        var statsJson = string.Join(",", stats.Select(s =>
            $$"""
            {
              "name": "{{s.Name}}",
              "displayName": "{{s.Name}} display",
              "shortDisplayName": "{{s.Name}}",
              "description": "{{s.Name}} desc",
              "abbreviation": "{{s.Name}}",
              "type": "{{s.Name}}",
              "value": {{s.Value.ToString(CultureInfo.InvariantCulture)}},
              "displayValue": "{{s.DisplayValue}}"
            }
            """));

        return $$"""
        {
          "$ref": "{{DocumentRef}}",
          "id": "61534",
          "name": "Road",
          "abbreviation": "RD",
          "displayName": "Road Record",
          "shortDisplayName": "Road",
          "description": "Record on the road",
          "type": "{{type}}",
          "summary": "10-5",
          "displayValue": "10-5",
          "value": 0.667,
          "stats": [{{statsJson}}]
        }
        """;
    }

    private async Task<Guid> SeedCompetitorAsync()
    {
        var competitor = Fixture.Build<FootballCompetitionCompetitor>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.CompetitionId, Guid.NewGuid())
            .With(x => x.FranchiseSeasonId, Guid.NewGuid())
            .With(x => x.HomeAway, "home")
            .Create();
        await FootballDataContext.CompetitionCompetitors.AddAsync(competitor);
        await FootballDataContext.SaveChangesAsync();
        return competitor.Id;
    }

    private ProcessDocumentCommand BuildCommand(string parentId, string documentJson) =>
        Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorRecord)
            .With(x => x.Document, documentJson)
            .With(x => x.ParentId, parentId)
            .OmitAutoProperties()
            .Create();

    [Fact]
    public async Task WhenRecordDoesNotExist_CreatesRecordWithStats()
    {
        var competitorId = await SeedCompetitorAsync();
        var documentJson = BuildDocumentJson("road",
            ("wins", 10, "10"),
            ("losses", 5, "5"));

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRecordDocumentProcessor<TeamSportDataContext>>();
        await sut.ProcessAsync(BuildCommand(competitorId.ToString(), documentJson));

        var record = await FootballDataContext.CompetitionCompetitorRecords
            .AsNoTracking()
            .Include(r => r.Stats)
            .FirstOrDefaultAsync(r => r.CompetitionCompetitorId == competitorId);

        record.Should().NotBeNull();
        record!.Type.Should().Be("road");
        record.Stats.Should().HaveCount(2);
        record.Stats.Should().Contain(s => s.Name == "wins" && s.Value == 10);
        record.Stats.Should().Contain(s => s.Name == "losses" && s.Value == 5);
    }

    [Fact]
    public async Task WhenRecordExists_DiffMergePreservesStatIds_OnValueChange()
    {
        var competitorId = await SeedCompetitorAsync();

        // First call seeds the record + stats with initial values.
        var firstDoc = BuildDocumentJson("road",
            ("wins", 10, "10"),
            ("losses", 5, "5"));

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRecordDocumentProcessor<TeamSportDataContext>>();
        await sut.ProcessAsync(BuildCommand(competitorId.ToString(), firstDoc));
        // Simulate production: each message gets a fresh DI scope + DbContext.
        // Without this, the second ProcessAsync sees cached entities from the
        // first call's change tracker and EF's InMemory provider misbehaves
        // on the second SaveChanges.
        FootballDataContext.ChangeTracker.Clear();

        var statIdsBefore = (await FootballDataContext.CompetitionCompetitorRecordStats
            .AsNoTracking()
            .Where(s => s.CompetitionCompetitorRecord.CompetitionCompetitorId == competitorId)
            .ToListAsync())
            .ToDictionary(s => s.Name, s => s.Id);

        statIdsBefore.Should().HaveCount(2);

        // Second call ships the same stat NAMES but different VALUES. Under
        // diff-merge this should UPDATE in place — same row Ids. Under the
        // prior DELETE-INSERT pattern, Ids would be regenerated. The
        // ChangeTracker.Clear() above simulates the production "fresh DI
        // scope per message" semantic; reusing the same `sut` here is fine
        // because the processor is stateless apart from the DbContext.
        var secondDoc = BuildDocumentJson("road",
            ("wins", 11, "11"),
            ("losses", 5, "5"));

        await sut.ProcessAsync(BuildCommand(competitorId.ToString(), secondDoc));

        var statsAfter = await FootballDataContext.CompetitionCompetitorRecordStats
            .AsNoTracking()
            .Where(s => s.CompetitionCompetitorRecord.CompetitionCompetitorId == competitorId)
            .ToListAsync();

        statsAfter.Should().HaveCount(2);

        var winsAfter = statsAfter.Single(s => s.Name == "wins");
        winsAfter.Id.Should().Be(statIdsBefore["wins"], "diff-merge must update in place, not delete-and-insert");
        winsAfter.Value.Should().Be(11);

        var lossesAfter = statsAfter.Single(s => s.Name == "losses");
        lossesAfter.Id.Should().Be(statIdsBefore["losses"], "unchanged stats must keep their Id under diff-merge");
        lossesAfter.Value.Should().Be(5);
    }

    [Fact]
    public async Task WhenRecordExists_AddsNewIncomingStats()
    {
        var competitorId = await SeedCompetitorAsync();
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRecordDocumentProcessor<TeamSportDataContext>>();

        // Seed with two stats.
        await sut.ProcessAsync(BuildCommand(competitorId.ToString(),
            BuildDocumentJson("road", ("wins", 10, "10"), ("losses", 5, "5"))));
        FootballDataContext.ChangeTracker.Clear();

        // Second call adds a third stat the DB doesn't know about.
        await sut.ProcessAsync(BuildCommand(competitorId.ToString(),
            BuildDocumentJson("road",
                ("wins", 10, "10"),
                ("losses", 5, "5"),
                ("winPercent", 0.667, ".667"))));

        var stats = await FootballDataContext.CompetitionCompetitorRecordStats
            .AsNoTracking()
            .Where(s => s.CompetitionCompetitorRecord.CompetitionCompetitorId == competitorId)
            .ToListAsync();

        stats.Should().HaveCount(3);
        stats.Should().Contain(s => s.Name == "winPercent" && s.Value == 0.667);
    }

    [Fact]
    public async Task WhenRecordExists_RemovesOrphanStats_NotInIncoming()
    {
        var competitorId = await SeedCompetitorAsync();
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRecordDocumentProcessor<TeamSportDataContext>>();

        // Seed with three stats.
        await sut.ProcessAsync(BuildCommand(competitorId.ToString(),
            BuildDocumentJson("road",
                ("wins", 10, "10"),
                ("losses", 5, "5"),
                ("ties", 0, "0"))));
        FootballDataContext.ChangeTracker.Clear();

        // Second call drops 'ties' — should be removed as an orphan.
        await sut.ProcessAsync(BuildCommand(competitorId.ToString(),
            BuildDocumentJson("road", ("wins", 10, "10"), ("losses", 5, "5"))));

        var stats = await FootballDataContext.CompetitionCompetitorRecordStats
            .AsNoTracking()
            .Where(s => s.CompetitionCompetitorRecord.CompetitionCompetitorId == competitorId)
            .ToListAsync();

        stats.Should().HaveCount(2);
        stats.Should().NotContain(s => s.Name == "ties");
    }

    [Fact]
    public async Task WhenCompetitorMissing_ReturnsWithoutCreatingRecord()
    {
        // No competitor seeded — ParentId references a nonexistent Id.
        var phantomCompetitorId = Guid.NewGuid();
        var documentJson = BuildDocumentJson("road", ("wins", 10, "10"));

        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRecordDocumentProcessor<TeamSportDataContext>>();
        await sut.ProcessAsync(BuildCommand(phantomCompetitorId.ToString(), documentJson));

        var anyRecords = await FootballDataContext.CompetitionCompetitorRecords
            .AsNoTracking()
            .AnyAsync(r => r.CompetitionCompetitorId == phantomCompetitorId);

        anyRecords.Should().BeFalse();
    }
}
