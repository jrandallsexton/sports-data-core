using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Api.Application.Previews.Commands.RejectMatchupPreview;

public interface IRejectMatchupPreviewCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        RejectMatchupPreviewCommand command,
        CancellationToken cancellationToken = default);
}

public class RejectMatchupPreviewCommandHandler : IRejectMatchupPreviewCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILeagueWeekMatchupsCacheInvalidator _cacheInvalidator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RejectMatchupPreviewCommandHandler(
        AppDataContext dataContext,
        ILeagueWeekMatchupsCacheInvalidator cacheInvalidator,
        IDateTimeProvider dateTimeProvider)
    {
        _dataContext = dataContext;
        _cacheInvalidator = cacheInvalidator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        RejectMatchupPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _dataContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == command.RejectedByUserId, cancellationToken);

        if (!userExists)
        {
            return new Failure<Guid>(
                command.PreviewId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.RejectedByUserId), "User not found")]);
        }

        var preview = await _dataContext.MatchupPreviews
            .FirstOrDefaultAsync(x => x.Id == command.PreviewId &&
                                      x.ContestId == command.ContestId, cancellationToken);

        if (preview is null)
        {
            return new Failure<Guid>(
                command.PreviewId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.PreviewId), "Preview not found.")]);
        }

        preview.RejectedUtc = _dateTimeProvider.UtcNow();
        preview.RejectionNote = command.RejectionNote;
        preview.ModifiedBy = command.RejectedByUserId;

        await _dataContext.SaveChangesAsync(cancellationToken);

        // A rejected preview is no longer "available" in the cached league-week
        // payload either - both flags there derive from RejectedUtc.
        await _cacheInvalidator.EvictForContestAsync(preview.ContestId, cancellationToken);

        return new Success<Guid>(preview.Id);
    }
}
