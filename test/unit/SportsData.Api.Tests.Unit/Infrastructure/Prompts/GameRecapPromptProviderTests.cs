using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Infrastructure.Blobs;

using Xunit;

namespace SportsData.Api.Tests.Unit.Infrastructure.Prompts;

public class GameRecapPromptProviderTests
{
    [Fact]
    public async Task GetGameRecapPromptAsync_ShouldCacheResult_AndReuseIt()
    {
        // Arrange
        var mockBlobStorage = new Mock<IProvideBlobStorage>();
        mockBlobStorage
            .Setup(x => x.GetFileContentsAsync("prompts", "game-recap-v1.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test prompt content");

        var serviceProvider = CreateServiceProvider(mockBlobStorage.Object);
        var provider = new GameRecapPromptProvider(serviceProvider);

        // Act
        var valueTask1 = provider.GetGameRecapPromptAsync(CancellationToken.None);
        var result1 = await valueTask1;
        var valueTask2 = provider.GetGameRecapPromptAsync(CancellationToken.None);
        var result2 = await valueTask2;

        // Assert
        result1.PromptText.Should().Be("Test prompt content");
        result1.PromptName.Should().Be("game-recap-v1");
        result2.Should().Be(result1);
        
        // Second call should return synchronously (ValueTask completed)
        valueTask2.IsCompletedSuccessfully.Should().BeTrue();

        // Verify blob storage was only called once (cached)
        mockBlobStorage.Verify(
            x => x.GetFileContentsAsync("prompts", "game-recap-v1.txt", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetGameRecapPromptAsync_ShouldRespectCallerCancellationToken()
    {
        // Arrange
        var callCount = 0;
        var mockBlobStorage = new Mock<IProvideBlobStorage>();
        mockBlobStorage
            .Setup(x => x.GetFileContentsAsync("prompts", "game-recap-v1.txt", It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>(async (container, blob, ct) =>
            {
                callCount++;
                await Task.Delay(100, ct); // Simulate async work
                return "Test prompt content";
            });

        var serviceProvider = CreateServiceProvider(mockBlobStorage.Object);
        var provider = new GameRecapPromptProvider(serviceProvider);

        using var cts1 = new CancellationTokenSource();
        using var cts2 = new CancellationTokenSource();

        // Act - First call uses cts1 token
        var task1 = provider.GetGameRecapPromptAsync(cts1.Token);
        
        // Cancel first token before completion
        cts1.Cancel();

        // Second call uses different token - should not fail due to first caller's cancellation
        var result2 = await provider.GetGameRecapPromptAsync(cts2.Token);

        // Assert
        result2.PromptText.Should().Be("Test prompt content");
        callCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetGameRecapPromptAsync_ShouldNotCacheOnFailure()
    {
        // Arrange
        var callCount = 0;
        var mockBlobStorage = new Mock<IProvideBlobStorage>();
        mockBlobStorage
            .Setup(x => x.GetFileContentsAsync("prompts", "game-recap-v1.txt", It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((container, blob, ct) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Blob storage error");
                return Task.FromResult("Test prompt content");
            });

        var serviceProvider = CreateServiceProvider(mockBlobStorage.Object);
        var provider = new GameRecapPromptProvider(serviceProvider);

        // Act & Assert - First call should throw
        await Assert.ThrowsAsync<Exception>(
            () => provider.GetGameRecapPromptAsync(CancellationToken.None).AsTask());

        // Second call should succeed (not cached from failure)
        var result = await provider.GetGameRecapPromptAsync(CancellationToken.None);

        result.PromptText.Should().Be("Test prompt content");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ReloadPromptAsync_ShouldClearCache_AndReturnNewPrompt()
    {
        // Arrange
        var callCount = 0;
        var mockBlobStorage = new Mock<IProvideBlobStorage>();
        mockBlobStorage
            .Setup(x => x.GetFileContentsAsync("prompts", "game-recap-v1.txt", It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((container, blob, ct) =>
            {
                callCount++;
                return Task.FromResult($"Prompt version {callCount}");
            });

        var serviceProvider = CreateServiceProvider(mockBlobStorage.Object);
        var provider = new GameRecapPromptProvider(serviceProvider);

        // Act
        var initial = await provider.GetGameRecapPromptAsync(CancellationToken.None);
        var reloaded = await provider.ReloadPromptAsync(CancellationToken.None);
        var afterReload = await provider.GetGameRecapPromptAsync(CancellationToken.None);

        // Assert
        initial.PromptText.Should().Be("Prompt version 1");
        reloaded.Should().Be("Prompt version 2");
        afterReload.PromptText.Should().Be("Prompt version 2");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ReloadPromptAsync_ShouldNotCacheOnFailure()
    {
        // Arrange
        var callCount = 0;
        var mockBlobStorage = new Mock<IProvideBlobStorage>();
        mockBlobStorage
            .Setup(x => x.GetFileContentsAsync("prompts", "game-recap-v1.txt", It.IsAny<CancellationToken>()))
            .Returns<string, string, CancellationToken>((container, blob, ct) =>
            {
                callCount++;
                if (callCount == 2)
                    throw new Exception("Reload failed");
                return Task.FromResult($"Prompt version {callCount}");
            });

        var serviceProvider = CreateServiceProvider(mockBlobStorage.Object);
        var provider = new GameRecapPromptProvider(serviceProvider);

        // Act
        var initial = await provider.GetGameRecapPromptAsync(CancellationToken.None);
        
        // Reload should fail
        await Assert.ThrowsAsync<Exception>(
            async () => await provider.ReloadPromptAsync(CancellationToken.None));

        // Next call should get a fresh load (cache was cleared by reload attempt)
        var result = await provider.GetGameRecapPromptAsync(CancellationToken.None);

        // Assert
        initial.PromptText.Should().Be("Prompt version 1");
        result.PromptText.Should().Be("Prompt version 3");
        callCount.Should().Be(3);
    }

    private static IServiceProvider CreateServiceProvider(IProvideBlobStorage blobStorage)
    {
        var services = new ServiceCollection();
        services.AddSingleton(blobStorage);
        return services.BuildServiceProvider();
    }
}
