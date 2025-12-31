using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Messageboard.Helpers;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Messageboard.Commands.ToggleReaction;

public interface IToggleReactionCommandHandler
{
    Task<Result<ReactionType?>> ExecuteAsync(
        ToggleReactionCommand command,
        CancellationToken cancellationToken = default);
}

public class ToggleReactionCommandHandler : IToggleReactionCommandHandler
{
    private readonly AppDataContext _dataContext;

    public ToggleReactionCommandHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<ReactionType?>> ExecuteAsync(
        ToggleReactionCommand command,
        CancellationToken cancellationToken = default)
    {
        var post = await _dataContext.Set<MessagePost>()
            .SingleOrDefaultAsync(p => p.Id == command.PostId, cancellationToken);

        if (post is null)
        {
            return new Failure<ReactionType?>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.PostId), "Post not found.")]);
        }

        var existing = await _dataContext.Set<MessageReaction>()
            .SingleOrDefaultAsync(
                r => r.PostId == command.PostId && r.UserId == command.UserId,
                cancellationToken);

        // Clear reaction (explicit remove)
        if (command.Type is null)
        {
            if (existing is not null)
            {
                MessageboardHelpers.AdjustReactionCounts(post, existing.Type, decrement: true);
                _dataContext.Remove(existing);
                _dataContext.Update(post);
                await _dataContext.SaveChangesAsync(cancellationToken);
            }

            return new Success<ReactionType?>(null);
        }

        // Upsert / toggle / flip
        ReactionType? resultType;

        if (existing is null)
        {
            // Add new reaction
            _dataContext.Add(new MessageReaction
            {
                Id = Guid.NewGuid(),
                PostId = command.PostId,
                UserId = command.UserId,
                Type = command.Type.Value,
                CreatedBy = command.UserId,
                CreatedUtc = DateTime.UtcNow
            });
            MessageboardHelpers.AdjustReactionCounts(post, command.Type.Value, decrement: false);
            resultType = command.Type.Value;
        }
        else if (existing.Type == command.Type.Value)
        {
            // Toggle off - same reaction clicked again
            MessageboardHelpers.AdjustReactionCounts(post, existing.Type, decrement: true);
            _dataContext.Remove(existing);
            resultType = null;
        }
        else
        {
            // Flip - change to different reaction type
            MessageboardHelpers.AdjustReactionCounts(post, existing.Type, decrement: true);
            existing.Type = command.Type.Value;
            existing.ModifiedBy = command.UserId;
            existing.ModifiedUtc = DateTime.UtcNow;
            _dataContext.Update(existing);
            MessageboardHelpers.AdjustReactionCounts(post, command.Type.Value, decrement: false);
            resultType = command.Type.Value;
        }

        _dataContext.Update(post);
        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<ReactionType?>(resultType);
    }
}
