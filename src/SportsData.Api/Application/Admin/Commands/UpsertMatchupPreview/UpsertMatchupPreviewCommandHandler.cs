using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.Admin.Commands.UpsertMatchupPreview;

public interface IUpsertMatchupPreviewCommandHandler
{
    /// <summary>
/// Validates the provided command and upserts the contained matchup preview, returning the associated contest identifier on success.
/// </summary>
/// <param name="command">The upsert command containing JSON preview content and related metadata.</param>
/// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
/// <returns>A Result containing the contest ID of the upserted matchup preview on success; on failure contains validation or error details.</returns>
Task<Result<Guid>> ExecuteAsync(UpsertMatchupPreviewCommand command, CancellationToken cancellationToken);
}

public class UpsertMatchupPreviewCommandHandler : IUpsertMatchupPreviewCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IValidator<UpsertMatchupPreviewCommand> _validator;
    private readonly ILogger<UpsertMatchupPreviewCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of UpsertMatchupPreviewCommandHandler with the required data context, validator, and logger.
    /// </summary>
    public UpsertMatchupPreviewCommandHandler(
        AppDataContext dataContext,
        IValidator<UpsertMatchupPreviewCommand> validator,
        ILogger<UpsertMatchupPreviewCommandHandler> logger)
    {
        _dataContext = dataContext;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Upserts a matchup preview from the provided command and returns the preview's ContestId on success.
    /// </summary>
    /// <param name="command">Command containing the JSON representation of the MatchupPreview to upsert.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>Success containing the preview's ContestId when the upsert completes; Failure with ResultStatus.Validation if validation fails or the JSON content is invalid; Failure with ResultStatus.Error for unexpected errors.</returns>
    public async Task<Result<Guid>> ExecuteAsync(UpsertMatchupPreviewCommand command, CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.Validation,
                validationResult.Errors);
        }

        try
        {
            var preview = command.JsonContent.FromJson<MatchupPreview>();

            if (preview is null)
            {
                _logger.LogWarning("Invalid preview content provided");
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(command.JsonContent), "Invalid preview content")]);
            }

            // Use explicit transaction to ensure atomicity of the upsert operation
            await using var transaction = await _dataContext.Database.BeginTransactionAsync(cancellationToken);

            var existing = await _dataContext.MatchupPreviews
                .FirstOrDefaultAsync(x => x.ContestId == preview.ContestId, cancellationToken);

            if (existing is not null)
                _dataContext.MatchupPreviews.Remove(existing);

            await _dataContext.MatchupPreviews.AddAsync(preview, cancellationToken);
            await _dataContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Upserted matchup preview for contest {ContestId}", preview.ContestId);

            return new Success<Guid>(preview.ContestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting matchup preview");
            return new Failure<Guid>(
                default,
                ResultStatus.Error,
                [new ValidationFailure("Error", "An error occurred while upserting the matchup preview")]);
        }
    }
}