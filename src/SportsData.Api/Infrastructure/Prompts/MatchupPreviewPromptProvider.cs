using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Infrastructure.Blobs;

namespace SportsData.Api.Infrastructure.Prompts;

public class MatchupPreviewPromptProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();
    private Task<string>? _cachedPromptTask;

    private const string Container = "prompts";
    private const string Blob = "prediction-insights-v1.txt";

    public MatchupPreviewPromptProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<string> GetPreviewInsightPromptAsync()
    {
        if (_cachedPromptTask != null)
            return _cachedPromptTask;

        lock (_lock)
        {
            return _cachedPromptTask ??= LoadPromptAsync();
        }
    }

    public async Task<string> ReloadPromptAsync()
    {
        var newPrompt = await LoadPromptAsync(forceReload: true);

        lock (_lock)
        {
            _cachedPromptTask = Task.FromResult(newPrompt);
        }

        return newPrompt;
    }

    private async Task<string> LoadPromptAsync(bool forceReload = false)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        return await blobStorage.GetFileContentsAsync(Container, Blob);
    }
}