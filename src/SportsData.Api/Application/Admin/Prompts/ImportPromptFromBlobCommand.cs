using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Blobs;

namespace SportsData.Api.Application.Admin.Prompts;

/// <summary>
/// One-time seeding path: pull a legacy prompt blob out of the "prompts"
/// container and create the Prompt entity from it. After the blobs are
/// imported, blob storage is out of the preview pipeline entirely.
/// </summary>
public class ImportPromptFromBlobCommand
{
    /// <summary>Blob name, with or without the .txt extension.</summary>
    public required string BlobName { get; set; }

    /// <summary>Prompt name; defaults to the blob name without extension (the legacy PromptVersion value).</summary>
    public string? Name { get; set; }

    public Sport? Sport { get; set; }

    public bool WithStats { get; set; }

    public bool IsDefault { get; set; }

    public string? Description { get; set; }
}

public interface IImportPromptFromBlobCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(ImportPromptFromBlobCommand command, CancellationToken cancellationToken);
}

public class ImportPromptFromBlobCommandHandler : IImportPromptFromBlobCommandHandler
{
    private const string Container = "prompts";

    private readonly IProvideBlobStorage _blobStorage;
    private readonly ICreatePromptCommandHandler _createHandler;
    private readonly ILogger<ImportPromptFromBlobCommandHandler> _logger;

    public ImportPromptFromBlobCommandHandler(
        IProvideBlobStorage blobStorage,
        ICreatePromptCommandHandler createHandler,
        ILogger<ImportPromptFromBlobCommandHandler> logger)
    {
        _blobStorage = blobStorage;
        _createHandler = createHandler;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(ImportPromptFromBlobCommand command, CancellationToken cancellationToken)
    {
        var blobName = command.BlobName.Trim();
        if (!blobName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            blobName += ".txt";

        var text = await _blobStorage.GetFileContentsAsync(Container, blobName, cancellationToken);

        _logger.LogInformation(
            "Importing prompt blob {BlobName} ({Length} chars) into the Prompt table.",
            blobName, text.Length);

        return await _createHandler.ExecuteAsync(new CreatePromptCommand
        {
            Name = command.Name?.Trim() ?? Path.GetFileNameWithoutExtension(blobName),
            Sport = command.Sport,
            WithStats = command.WithStats,
            IsDefault = command.IsDefault,
            Description = command.Description ?? $"Imported from blob '{blobName}'",
            Text = text
        }, cancellationToken);
    }
}
