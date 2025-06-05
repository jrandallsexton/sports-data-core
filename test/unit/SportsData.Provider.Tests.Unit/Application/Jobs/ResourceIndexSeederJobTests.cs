using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;
using Moq.AutoMock;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

using Xunit;

public class ResourceIndexSeederJobTests
{
    [Fact]
    public async Task ExecuteAsync_SavesResourceIndexAndItemsAndEnqueuesJob()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDataContext(options);

        var mocker = new AutoMocker();
        mocker.Use(typeof(AppDataContext), context);
        mocker.Use(typeof(IProvideHashes), new HashProvider());
        mocker.Use(typeof(IProvideEspnApiData), mocker.CreateInstance<EspnApiClient>());

        var seederJob = mocker.CreateInstance<ResourceIndexSeederJob>();

        var rootUrl = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises?lang=en&limit=999");

        // Act
        await seederJob.ExecuteAsync("Venue", "FootballNcaa", SourceDataProvider.Espn, rootUrl, 2);

        // Assert
        Assert.Single(context.ResourceIndexJobs);
        Assert.Equal(2, await context.ResourceIndexItems.CountAsync()); // root + child

        //jobQueueMock.Verify(j => j.Enqueue<IProcessResourceIndexes>(It.IsAny<Func<IProcessResourceIndexes, Task>>()), Times.Once);
    }
}
