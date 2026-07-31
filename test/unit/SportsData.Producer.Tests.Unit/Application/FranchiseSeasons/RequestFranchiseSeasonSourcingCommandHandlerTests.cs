using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.FranchiseSeasons.Commands.RequestFranchiseSeasonSourcing;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons;

/// <summary>
/// Bulk sourcing fan-out: one DocumentRequested (TeamSeason, full cascade)
/// per franchise season with a usable ESPN ref; the rest are counted and
/// skipped rather than failing the batch.
/// </summary>
public class RequestFranchiseSeasonSourcingCommandHandlerTests
    : ProducerTestBase<RequestFranchiseSeasonSourcingCommandHandler>
{
    private const int SeasonYear = 2026;

#nullable enable
    private async Task<FranchiseSeason> SeedFranchiseSeasonAsync(string? sourceUrl)
    {
        var franchiseId = Guid.NewGuid();
        var fsId = Guid.NewGuid();

        await FootballDataContext.Franchises.AddAsync(new Franchise
        {
            Id = franchiseId,
            Sport = Sport.FootballNfl,
            Name = $"Team {franchiseId:N}"[..12],
            Nickname = "Testers",
            Location = "Testville",
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            Slug = $"team-{franchiseId:N}"[..20],
            ColorCodeHex = "000000",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        var fs = new FranchiseSeason
        {
            Id = fsId,
            FranchiseId = franchiseId,
            SeasonYear = SeasonYear,
            Slug = $"fs-{fsId:N}"[..20],
            Location = "Testville",
            Name = "Test Team",
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            ColorCodeHex = "000000",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        if (sourceUrl is not null)
        {
            fs.ExternalIds.Add(new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = fsId,
                Provider = SourceDataProvider.Espn,
                Value = fsId.ToString(),
                SourceUrlHash = fsId.ToString("N"),
                SourceUrl = sourceUrl,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            });
        }

        await FootballDataContext.FranchiseSeasons.AddAsync(fs);
        await FootballDataContext.SaveChangesAsync();
        return fs;
    }

    private RequestFranchiseSeasonSourcingCommandHandler CreateHandler()
    {
        Mocker.GetMock<IGenerateExternalRefIdentities>()
            .Setup(x => x.Generate(It.IsAny<Uri>()))
            .Returns((Uri u) => new ExternalRefIdentity(
                Guid.NewGuid(),
                u.ToString().GetHashCode().ToString("X"),
                u.ToString()));
        return Mocker.CreateInstance<RequestFranchiseSeasonSourcingCommandHandler>();
    }

    [Fact]
    public async Task PublishesOneRequestPerFranchiseSeason_FullCascade()
    {
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/1");
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/2");

        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(SeasonYear, Sport.FootballNfl));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.DocumentType == DocumentType.TeamSeason &&
                    e.Sport == Sport.FootballNfl &&
                    e.SeasonYear == SeasonYear &&
                    // Full cascade: no linked-type filter (it would propagate
                    // down the tree and strangle the schedule's children).
                    e.IncludeLinkedDocumentTypes == null),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SkipsFranchiseSeasonsWithoutUsableRef_ContinuesBatch()
    {
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/1");
        await SeedFranchiseSeasonAsync(sourceUrl: null);
        await SeedFranchiseSeasonAsync(sourceUrl: "not-a-uri");

        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(SeasonYear, Sport.FootballNfl));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoFranchiseSeasons_ReturnsNotFound_PublishesNothing()
    {
        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(SeasonYear, Sport.FootballNfl));

        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
