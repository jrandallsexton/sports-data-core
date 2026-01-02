using SportsData.Core.Infrastructure.Blobs;

namespace SportsData.Api.Infrastructure.Prompts;

/// <summary>
/// Provides game recap prompts from Azure Blob Storage with caching
/// </summary>
public class GameRecapPromptProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();
    private Task<(string PromptText, string PromptName)>? _cachedPromptTask;

    private const string Container = "prompts";
    private const string BlobName = "game-recap-v1.txt";

    public GameRecapPromptProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the game recap prompt (cached after first load)
    /// </summary>
    public Task<(string PromptText, string PromptName)> GetGameRecapPromptAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedPromptTask != null)
            return _cachedPromptTask;

        lock (_lock)
        {
            return _cachedPromptTask ??= LoadPromptAsync(BlobName, cancellationToken);
        }
    }

    /// <summary>
    /// Forces a reload of the prompt from blob storage (useful for testing prompt changes)
    /// </summary>
    public async Task<string> ReloadPromptAsync(CancellationToken cancellationToken = default)
    {
        var newPrompt = await LoadPromptTextOnlyAsync(BlobName, cancellationToken);

        lock (_lock)
        {
            _cachedPromptTask = Task.FromResult((newPrompt, Path.GetFileNameWithoutExtension(BlobName)));
        }

        return newPrompt;
    }

    private async Task<(string, string)> LoadPromptAsync(string blobName, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        var promptText = await blobStorage.GetFileContentsAsync(Container, blobName, cancellationToken);
        var promptName = Path.GetFileNameWithoutExtension(blobName);

        return (promptText, promptName);
    }

    private async Task<string> LoadPromptTextOnlyAsync(string blobName, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        return await blobStorage.GetFileContentsAsync(Container, blobName, cancellationToken);
    }
}
