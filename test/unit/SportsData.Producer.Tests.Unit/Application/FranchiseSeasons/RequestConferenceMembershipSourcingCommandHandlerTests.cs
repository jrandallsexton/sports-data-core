using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.FranchiseSeasons.Commands.RequestConferenceMembershipSourcing;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons;

/// <summary>
/// Conference membership backfill: seeds the season's root group(s), in the
/// season-type variant its team-seasons already use, with the explicit
/// [GroupSeason, TeamSeason] filter that makes an existing group walk its
/// children and member lists.
/// </summary>
public class RequestConferenceMembershipSourcingCommandHandlerTests
    : ProducerTestBase<RequestConferenceMembershipSourcingCommandHandler>
{
    private const int SeasonYear = 2005;
    private const string LeagueBase = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football";

    // Fixed clock: deterministic seed data, per the no-DateTime.UtcNow rule.
    private static readonly DateTime FixedNow = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SeedsOnlyTheRootOfTheVariantInUse_WithTheMembershipFilter()
    {
        var rootType2 = await SeedGroupAsync(seasonType: 2, espnGroupId: 99, parentId: null);
        var rootType3 = await SeedGroupAsync(seasonType: 3, espnGroupId: 99, parentId: null);
        var conferenceType3 = await SeedGroupAsync(seasonType: 3, espnGroupId: 17, parentId: rootType3.Id);
        var conferenceType2 = await SeedGroupAsync(seasonType: 2, espnGroupId: 17, parentId: rootType2.Id);

        // Two team-seasons on types/3, one on types/2: types/3 is in use.
        await SeedFranchiseSeasonAsync(conferenceType3.Id);
        await SeedFranchiseSeasonAsync(conferenceType3.Id);
        await SeedFranchiseSeasonAsync(conferenceType2.Id);

        var published = CapturePublished();
        var correlationId = Guid.NewGuid();

        var result = await CreateHandler().ExecuteAsync(
            new RequestConferenceMembershipSourcingCommand(SeasonYear, Sport.FootballNcaa, correlationId));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(correlationId);

        published.Should().ContainSingle();
        var request = published[0];
        request.DocumentType.Should().Be(DocumentType.GroupSeason);
        request.Uri.AbsolutePath.Should().EndWith("/seasons/2005/types/3/groups/99");
        request.CorrelationId.Should().Be(correlationId);
        request.IncludeLinkedDocumentTypes.Should().BeEquivalentTo(
            new[] { DocumentType.GroupSeason, DocumentType.TeamSeason });

        Mocker.GetMock<IMessageDeliveryScope>().Verify(x => x.Use(DeliveryMode.Direct), Times.Once);
    }

    [Fact]
    public async Task NoGroupedTeamSeasons_ReturnsNotFoundAndPublishesNothing()
    {
        await SeedGroupAsync(seasonType: 3, espnGroupId: 99, parentId: null);
        var published = CapturePublished();

        var result = await CreateHandler().ExecuteAsync(
            new RequestConferenceMembershipSourcingCommand(SeasonYear, Sport.FootballNcaa, Guid.NewGuid()));

        // A Failure, so ToActionResult maps it to 404 rather than 200 + Guid.Empty.
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        published.Should().BeEmpty();
    }

    [Fact]
    public async Task SeasonBeforeTheFloor_IsRejected()
    {
        var result = await CreateHandler().ExecuteAsync(
            new RequestConferenceMembershipSourcingCommand(1999, Sport.FootballNcaa, Guid.NewGuid()));

        result.Status.Should().Be(ResultStatus.Validation);
    }

    private async Task<GroupSeason> SeedGroupAsync(int seasonType, int espnGroupId, Guid? parentId)
    {
        var url = $"{LeagueBase}/seasons/{SeasonYear}/types/{seasonType}/groups/{espnGroupId}";
        var id = Guid.NewGuid();

        var group = new GroupSeason
        {
            Id = id,
            ParentId = parentId,
            SeasonYear = SeasonYear,
            Name = $"Group {espnGroupId}",
            Slug = $"group-{espnGroupId}",
            Abbreviation = $"g{espnGroupId}",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.Empty,
            ExternalIds =
            [
                new GroupSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    GroupSeasonId = id,
                    Provider = SourceDataProvider.Espn,
                    Value = url,
                    SourceUrl = url,
                    SourceUrlHash = url.GetHashCode().ToString("X"),
                    CreatedUtc = FixedNow,
                    CreatedBy = Guid.Empty
                }
            ]
        };

        await FootballDataContext.GroupSeasons.AddAsync(group);
        await FootballDataContext.SaveChangesAsync();
        return group;
    }

    private async Task SeedFranchiseSeasonAsync(Guid groupSeasonId)
    {
        var id = Guid.NewGuid();

        await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
        {
            Id = id,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = SeasonYear,
            GroupSeasonId = groupSeasonId,
            Slug = $"fs-{id:N}"[..20],
            Location = "Testville",
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            ColorCodeHex = "000000",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.Empty
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private List<DocumentRequested> CapturePublished()
    {
        var published = new List<DocumentRequested>();
        Mocker.GetMock<IEventBus>()
            .Setup(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentRequested, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);
        return published;
    }

    private RequestConferenceMembershipSourcingCommandHandler CreateHandler()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<FluentValidation.IValidator<RequestConferenceMembershipSourcingCommand>>(
            new RequestConferenceMembershipSourcingCommandValidator(Mocker.Get<IDateTimeProvider>()));
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());
        Mocker.GetMock<IMessageDeliveryScope>()
            .Setup(x => x.Use(It.IsAny<DeliveryMode>()))
            .Returns(new NoopDisposable());
        return Mocker.CreateInstance<RequestConferenceMembershipSourcingCommandHandler>();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
