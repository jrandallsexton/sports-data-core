using Microsoft.Extensions.Logging;

using Moq;
using SportsData.Producer.Infrastructure.Data;
using Xunit;

namespace SportsData.Producer.Tests.Unit.Infrastructure.Data;

public class AzureVmDatabaseScalerTests
{
    private const string SubscriptionId = "foo";
    private const string ResourceGroup = "bar";
    private const string VmName = "zimZam";
    private const string ScaleUpSize = "Standard_B2s";
    private const string ScaleDownSize = "Standard_B1s";

    [Fact(Skip = "Manual test only. Scales a real VM up.")]
    public async Task ScaleUpAsync_ShouldResizeVm()
    {
        var loggerMock = new Mock<ILogger<AzureVmDatabaseScaler>>();
        var scaler = new AzureVmDatabaseScaler(
            SubscriptionId,
            ResourceGroup,
            VmName,
            ScaleUpSize,
            ScaleDownSize,
            loggerMock.Object);

        await scaler.ScaleUpAsync("Test scale-up", CancellationToken.None);
    }

    [Fact(Skip = "Manual test only. Scales a real VM down.")]
    public async Task ScaleDownAsync_ShouldResizeVm()
    {
        var loggerMock = new Mock<ILogger<AzureVmDatabaseScaler>>();
        var scaler = new AzureVmDatabaseScaler(
            SubscriptionId,
            ResourceGroup,
            VmName,
            ScaleUpSize,
            ScaleDownSize,
            loggerMock.Object);

        await scaler.ScaleDownAsync("Test scale-down", CancellationToken.None);
    }
}