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

    public CreateReplyCommandHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
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
}
