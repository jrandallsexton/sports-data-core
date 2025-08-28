using SportsData.Core.Infrastructure.Blobs;

namespace SportsData.Api.Infrastructure.Prompts;

public class MatchupPreviewPromptProvider
{
    private readonly IProvideBlobStorage _blobStorage;
    private readonly object _lock = new();
    private Task<string>? _cachedPromptTask;

    private const string Container = "prompts";
    private const string Blob = "prediction-insights-v1.txt";

    public MatchupPreviewPromptProvider(IProvideBlobStorage blobStorage)
    {
        _blobStorage = blobStorage;
    }

    public Task<string> GetPreviewInsightPromptAsync()
    {
        if (_cachedPromptTask != null)
            return _cachedPromptTask;

        lock (_lock)
        {
            return _cachedPromptTask ??= _blobStorage.GetFileContentsAsync(Container, Blob);
        }
    }

    public async Task<string> ReloadPromptAsync()
    {
        var newPrompt = await _blobStorage.GetFileContentsAsync(Container, Blob);

        lock (_lock)
        {
            _cachedPromptTask = Task.FromResult(newPrompt);
        }

        return newPrompt;
    }
}