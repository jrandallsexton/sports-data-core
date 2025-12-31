using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Messageboard.Commands.CreateThread;

public interface ICreateThreadCommandHandler
{
    Task<Result<MessageThread>> ExecuteAsync(
        CreateThreadCommand command,
        CancellationToken cancellationToken = default);
}

public class CreateThreadCommandHandler : ICreateThreadCommandHandler
{
    private readonly AppDataContext _dataContext;

    public CreateThreadCommandHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<MessageThread>> ExecuteAsync(
        CreateThreadCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Content))
        {
            return new Failure<MessageThread>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Content), "Content is required.")]);
        }

        var utcNow = DateTime.UtcNow;

        var thread = new MessageThread
        {
            Id = Guid.NewGuid(),
            GroupId = command.GroupId,
            CreatedBy = command.UserId,
            CreatedUtc = utcNow,
            LastActivityAt = utcNow,
            Title = string.IsNullOrWhiteSpace(command.Title) ? null : command.Title.Trim(),
            PostCount = 0
        };

        var rootPost = new MessagePost
        {
            Id = Guid.NewGuid(),
            ThreadId = thread.Id,
            ParentId = null,
            Depth = 0,
            Path = "0001",
            Content = command.Content.Trim(),
            CreatedBy = command.UserId,
            CreatedUtc = utcNow,
            ReplyCount = 0,
            LikeCount = 0,
            DislikeCount = 0
        };

        thread.Posts.Add(rootPost);
        thread.PostCount = 1;

        _dataContext.Add(thread);
        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<MessageThread>(thread);
    }
}
