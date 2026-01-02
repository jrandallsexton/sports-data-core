using SportsData.Core.Infrastructure.Blobs;

namespace SportsData.Api.Infrastructure.Prompts;

/// <summary>
/// Provides game recap prompts from Azure Blob Storage with caching
/// </summary>
public class GameRecapPromptProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();
    private (string PromptText, string PromptName)? _cachedPrompt;

    private const string Container = "prompts";
    private const string BlobName = "game-recap-v1.txt";

    public GameRecapPromptProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets the game recap prompt (cached after first load)
    /// </summary>
    public ValueTask<(string PromptText, string PromptName)> GetGameRecapPromptAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedPrompt.HasValue)
            return new ValueTask<(string PromptText, string PromptName)>(_cachedPrompt.Value);

        return new ValueTask<(string PromptText, string PromptName)>(LoadAndCachePromptAsync(cancellationToken));
    }

    private async Task<(string PromptText, string PromptName)> LoadAndCachePromptAsync(CancellationToken cancellationToken)
    {
        Task<(string, string)> loadTask;

        lock (_lock)
        {
            if (_cachedPrompt.HasValue)
                return _cachedPrompt.Value;

            loadTask = LoadPromptAsync(BlobName, cancellationToken);
        }

        var result = await loadTask;

        lock (_lock)
        {
            _cachedPrompt = result;
        }

        return result;
    }

    /// <summary>
    /// Forces a reload of the prompt from blob storage (useful for testing prompt changes)
    /// </summary>
    public async Task<string> ReloadPromptAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _cachedPrompt = null;
        }

        var newPrompt = await LoadPromptTextOnlyAsync(BlobName, cancellationToken);

        lock (_lock)
        {
            _cachedPrompt = (newPrompt, Path.GetFileNameWithoutExtension(BlobName));
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
