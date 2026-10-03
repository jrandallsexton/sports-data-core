using Moq;

using SportsData.Api.Application.Admin.Queries.GetModelLabMatrix;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Previews.Commands.GenerateMatchupPreviews;
using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Queries.GetModelLabMatrix;

public class GetModelLabMatrixQueryHandlerTests : ApiTestBase<GetModelLabMatrixQueryHandler>
{
    private static readonly DateTime Kickoff = new(2026, 9, 26, 19, 30, 0, DateTimeKind.Utc);

    private readonly Guid _contestId = Guid.NewGuid();
    private readonly Guid _awayId = Guid.NewGuid();
    private readonly Guid _homeId = Guid.NewGuid();
    private readonly Guid _modelId = Guid.NewGuid();
    private readonly Mock<IProvideContests> _contestClient = new();

    private async Task SeedWeekAsync()
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            CommissionerUserId = Guid.NewGuid()
        };
        var provider = new ModelProvider { Id = Guid.NewGuid(), Name = "Provider", Kind = ModelProviderKind.Anthropic };

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(), GroupId = group.Id, SeasonYear = 2026, SeasonWeek = 5, ContestId = _contestId
        });
        await DataContext.ModelProviders.AddAsync(provider);
        await DataContext.Models.AddAsync(new Model
        {
            Id = _modelId, Name = "Model A", ApiModelId = "model-a", ModelProviderId = provider.Id
        });
        await DataContext.SaveChangesAsync();

        Mocker.GetMock<IAiModelClientResolver>()
            .Setup(x => x.CanResolve(It.IsAny<ModelGateway>(), It.IsAny<ModelProviderKind>()))
            .Returns(true);

        _contestClient
            .Setup(x => x.GetMatchupsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<MarkDirection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<LeagueMatchupDto>>([new LeagueMatchupDto
            {
                ContestId = _contestId,
                StartDateUtc = Kickoff,
                Away = "Away Team", AwayShort = "AWAY", AwayFranchiseSeasonId = _awayId,
                Home = "Home Team", HomeShort = "HOME", HomeFranchiseSeasonId = _homeId
            }]));
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClient.Object);
    }

    private MatchupPreviewPrompt Capture(int? away, int? home, OverUnderPrediction? overUnder) => new()
    {
        Id = Guid.NewGuid(),
        ContestId = _contestId,
        Sport = Sport.FootballNcaa,
        Mode = PreviewGenerationMode.Experiment,
        ModelId = _modelId,
        PromptVersion = "prompt-v1",
        PromptText = "TEXT",
        PayloadJson = "{}",
        PredictedStraightUpWinnerId = _homeId,
        AwayScore = away,
        HomeScore = home,
        OverUnderPrediction = overUnder,
        CreatedUtc = Kickoff.AddDays(-2)
    };

    private static GetModelLabMatrixQuery Week5() => new() { Sport = Sport.FootballNcaa, SeasonYear = 2026, Week = 5 };

    [Fact]
    public async Task Cell_CarriesTheCapturesScoresAndOverUnder()
    {
        await SeedWeekAsync();
        await DataContext.MatchupPreviewPrompts.AddAsync(Capture(17, 31, OverUnderPrediction.Over));
        await DataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetModelLabMatrixQueryHandler>()
            .ExecuteAsync(Week5(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cell = Assert.Single(Assert.Single(result.Value.Contests).Cells);
        Assert.Equal(_modelId, cell.ModelId);
        Assert.Equal(_homeId, cell.PredictedStraightUpWinnerId);
        Assert.Equal(17, cell.AwayScore);
        Assert.Equal(31, cell.HomeScore);
        Assert.Equal(OverUnderPrediction.Over, cell.OverUnderPrediction);
    }

    [Fact]
    public async Task Cell_PassesNullsThrough_ForCapturesWrittenBeforeTheColumnsExisted()
    {
        // Captures older than the columns (and not backfilled) have NULLs;
        // the cell must carry null, not a default 0 / None.
        await SeedWeekAsync();
        await DataContext.MatchupPreviewPrompts.AddAsync(Capture(null, null, null));
        await DataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<GetModelLabMatrixQueryHandler>()
            .ExecuteAsync(Week5(), CancellationToken.None);

        var cell = Assert.Single(Assert.Single(result.Value.Contests).Cells);
        Assert.Null(cell.AwayScore);
        Assert.Null(cell.HomeScore);
        Assert.Null(cell.OverUnderPrediction);
    }
}
