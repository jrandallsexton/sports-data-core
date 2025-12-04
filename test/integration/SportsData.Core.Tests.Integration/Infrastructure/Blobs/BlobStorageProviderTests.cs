using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Infrastructure.Blobs;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Core.Tests.Integration.Infrastructure.Blobs;

public class BlobStorageProviderTests : IntegrationTestBase<BlobStorageProviderTests>
{
    private readonly IProvideBlobStorage _blobStorageProvider;

    public BlobStorageProviderTests() : base("Dev")
    {
        _blobStorageProvider = ActivatorUtilities.CreateInstance<BlobStorageProvider>(ServiceProvider);
    }

    [Fact]
    public async Task CanDownloadPromptText()
    {
        var result = await _blobStorageProvider.GetFileContentsAsync("prompts", "prediction-insights-v1.txt");

        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("plausible");
    }
}