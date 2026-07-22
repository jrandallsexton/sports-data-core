using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// Tests for the shared AthleteStatusResolver used by the AthleteSeason
/// processors (both sports) to populate AthleteSeason.StatusId.
/// </summary>
public class AthleteStatusResolverTests
    : ProducerTestBase<AthleteSeasonDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task ResolveIdAsync_CreatesStatus_WhenNoneExists()
    {
        var dto = new EspnAthleteStatusDto
        {
            Id = 2,
            Name = "Inactive",
            Type = "inactive",
            Abbreviation = "Inactive"
        };

        var id = await AthleteStatusResolver.ResolveIdAsync(FootballDataContext, dto);

        id.Should().NotBeNull();
        var created = await FootballDataContext.AthleteStatuses.AsNoTracking().SingleAsync();
        created.Id.Should().Be(id!.Value);
        created.Name.Should().Be("Inactive");
        created.NameNormalized.Should().Be("inactive");
        created.Abbreviation.Should().Be("Inactive");
        created.Type.Should().Be("inactive");
        created.ExternalId.Should().Be("2");
    }

    [Fact]
    public async Task ResolveIdAsync_IsCaseInsensitive_AndDoesNotDuplicate()
    {
        var first = await AthleteStatusResolver.ResolveIdAsync(
            FootballDataContext,
            new EspnAthleteStatusDto { Id = 1, Name = "Active", Type = "active", Abbreviation = "A" });

        // Different casing must resolve to the same row via the canonical
        // NameNormalized key, not create a second AthleteStatus.
        var second = await AthleteStatusResolver.ResolveIdAsync(
            FootballDataContext,
            new EspnAthleteStatusDto { Id = 1, Name = "ACTIVE", Type = "active", Abbreviation = "A" });

        second.Should().Be(first);
        (await FootballDataContext.AthleteStatuses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsExisting_AndDoesNotDuplicate()
    {
        var existing = new AthleteStatus
        {
            Id = Guid.NewGuid(),
            Name = "Active",
            NameNormalized = "active",
            Abbreviation = "A",
            Type = "active",
            ExternalId = "1"
        };
        await FootballDataContext.AthleteStatuses.AddAsync(existing);
        await FootballDataContext.SaveChangesAsync();
        FootballDataContext.ChangeTracker.Clear();

        // Same name, different casing — should match the existing row.
        var id = await AthleteStatusResolver.ResolveIdAsync(
            FootballDataContext,
            new EspnAthleteStatusDto { Id = 1, Name = "active", Type = "active", Abbreviation = "A" });

        id.Should().Be(existing.Id);
        (await FootballDataContext.AthleteStatuses.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsNull_WhenStatusOrNameMissing()
    {
        (await AthleteStatusResolver.ResolveIdAsync(FootballDataContext, null)).Should().BeNull();
        (await AthleteStatusResolver.ResolveIdAsync(
            FootballDataContext,
            new EspnAthleteStatusDto { Id = 0, Name = "" })).Should().BeNull();

        (await FootballDataContext.AthleteStatuses.AnyAsync()).Should().BeFalse();
    }
}
