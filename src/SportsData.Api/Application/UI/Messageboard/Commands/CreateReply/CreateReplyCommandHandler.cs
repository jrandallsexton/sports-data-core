using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Application.UI.Messageboard.Helpers;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Messageboard.Commands.CreateReply;

public interface ICreateReplyCommandHandler
{
    Task<Result<MessagePost>> ExecuteAsync(
        CreateReplyCommand command,
        CancellationToken cancellationToken = default);
}

public class CreateReplyCommandHandler : ICreateReplyCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILogger<CreateReplyCommandHandler> _logger;
    private const int MaxRetryAttempts = 3;

    public CreateReplyCommandHandler(
        AppDataContext dataContext,
        ILogger<CreateReplyCommandHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<MessagePost>> ExecuteAsync(
        CreateReplyCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            return new Failure<MessagePost>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Content), "Content is required.")]);
        }

        // Retry logic to handle unique constraint violations due to concurrent inserts
        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                return await TryCreateReplyAsync(command, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex) && attempt < MaxRetryAttempts - 1)
            {
                _logger.LogWarning(
                    ex,
                    "Unique constraint violation on attempt {Attempt} for ThreadId={ThreadId}, ParentId={ParentId}. Retrying...",
                    attempt + 1,
                    command.ThreadId,
                    command.ParentPostId);

                // Small delay before retry to reduce contention
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken);
            }
        }

        // If all retries failed, return error
        _logger.LogError(
            "Failed to create reply after {MaxAttempts} attempts due to concurrent path conflicts. ThreadId={ThreadId}, ParentId={ParentId}",
            MaxRetryAttempts,
            command.ThreadId,
            command.ParentPostId);

        return new Failure<MessagePost>(
            default!,
            ResultStatus.Error,
            [new ValidationFailure("Path", "Unable to create reply due to concurrent modifications. Please try again.")]);
    }

    private async Task<Result<MessagePost>> TryCreateReplyAsync(
        CreateReplyCommand command,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var thread = await _dataContext.Set<MessageThread>()
            .SingleOrDefaultAsync(t => t.Id == command.ThreadId, cancellationToken);

        if (thread is null)
        {
            return new Failure<MessagePost>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.ThreadId), "Thread not found.")]);
        }

        MessagePost? parent = null;
        if (command.ParentPostId is not null)
        {
            parent = await _dataContext.Set<MessagePost>()
                .SingleOrDefaultAsync(
                    p => p.Id == command.ParentPostId.Value && p.ThreadId == command.ThreadId,
                    cancellationToken);

            if (parent is null)
            {
                return new Failure<MessagePost>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(command.ParentPostId), "Parent post not found.")]);
            }
        }

        var depth = (parent?.Depth ?? 0) + 1;

        // Count siblings to generate next segment
        var siblingCount = await _dataContext.Set<MessagePost>()
            .Where(p => p.ThreadId == command.ThreadId && p.ParentId == command.ParentPostId)
            .CountAsync(cancellationToken);

        var segment = MessageboardHelpers.ToFixedBase36(siblingCount + 1, 4);
        var path = parent is null ? segment : $"{parent.Path}.{segment}";

        var reply = new MessagePost
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            ParentId = command.ParentPostId,
            Depth = depth,
            Path = path,
            Content = command.Content.Trim(),
            CreatedBy = command.UserId,
            CreatedUtc = utcNow,
            ReplyCount = 0,
            LikeCount = 0,
            DislikeCount = 0
        };

        _dataContext.Add(reply);

        if (parent is not null)
        {
            parent.ReplyCount += 1;
            _dataContext.Update(parent);
        }

        thread.PostCount += 1;
        thread.LastActivityAt = utcNow;
        _dataContext.Update(thread);

        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<MessagePost>(reply);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // Check for unique constraint violation
        // PostgreSQL: "23505" - unique_violation
        // SQL Server: error number 2601 or 2627
        var innerException = ex.InnerException;
        if (innerException is null)
            return false;

        var message = innerException.Message;

        // PostgreSQL
        if (message.Contains("23505") || message.Contains("duplicate key"))
            return true;

        // SQL Server
        if (message.Contains("2601") || message.Contains("2627") ||
            message.Contains("Cannot insert duplicate key") ||
            message.Contains("IX_MessagePost_ThreadId_Path_Unique"))
            return true;

        return false;
    }
}
