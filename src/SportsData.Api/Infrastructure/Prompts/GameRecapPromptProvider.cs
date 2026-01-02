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
    /// <summary>
    /// Get the cached game recap prompt, loading and caching it from blob storage if not already cached.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the underlying blob load when a cache miss requires fetching the prompt.</param>
    /// <returns>A tuple containing `PromptText` (the prompt content) and `PromptName` (the blob file name without its extension).</returns>
    public ValueTask<(string PromptText, string PromptName)> GetGameRecapPromptAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedPrompt.HasValue)
            return new ValueTask<(string PromptText, string PromptName)>(_cachedPrompt.Value);

        return new ValueTask<(string PromptText, string PromptName)>(LoadAndCachePromptAsync(cancellationToken));
    }

    /// <summary>
    /// Loads the prompt from blob storage if it is not already cached and updates the in-memory cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token used to cancel the underlying blob I/O operation.</param>
    /// <returns>A tuple containing the prompt text and the prompt name (the blob file name without extension).</returns>
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
    /// <summary>
    /// Force reloads the game recap prompt from blob storage and updates the in-memory cache.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for the reload operation to complete.</param>
    /// <returns>The reloaded prompt text.</returns>
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

    /// <summary>
    /// Load the prompt text from the configured blob container and derive the prompt name from the blob file name.
    /// </summary>
    /// <param name="blobName">The blob file name to load (including extension).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the blob I/O operation.</param>
    /// <returns>A tuple containing the prompt text and the prompt name (file name without extension).</returns>
    private async Task<(string, string)> LoadPromptAsync(string blobName, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        var promptText = await blobStorage.GetFileContentsAsync(Container, blobName, cancellationToken);
        var promptName = Path.GetFileNameWithoutExtension(blobName);

        return (promptText, promptName);
    }

    /// <summary>
    /// Load the prompt text for the specified blob from the configured prompts container in blob storage.
    /// </summary>
    /// <param name="blobName">The blob name (including extension) to read.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the I/O operation.</param>
    /// <returns>The contents of the blob as a string.</returns>
    private async Task<string> LoadPromptTextOnlyAsync(string blobName, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        return await blobStorage.GetFileContentsAsync(Container, blobName, cancellationToken);
    }
}