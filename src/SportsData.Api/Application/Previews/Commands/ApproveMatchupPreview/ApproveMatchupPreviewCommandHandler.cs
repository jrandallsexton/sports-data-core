using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Api.Application.Previews.Commands.ApproveMatchupPreview;

public interface IApproveMatchupPreviewCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        ApproveMatchupPreviewCommand command,
        CancellationToken cancellationToken = default);
}

public class ApproveMatchupPreviewCommandHandler : IApproveMatchupPreviewCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILeagueWeekMatchupsCacheInvalidator _cacheInvalidator;
    private readonly IEventBus _eventBus;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ApproveMatchupPreviewCommandHandler(
        AppDataContext dataContext,
        ILeagueWeekMatchupsCacheInvalidator cacheInvalidator,
        IEventBus eventBus,
        IDateTimeProvider dateTimeProvider)
    {
        _dataContext = dataContext;
        _cacheInvalidator = cacheInvalidator;
        _eventBus = eventBus;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        ApproveMatchupPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        var userExists = await _dataContext.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == command.ApprovedByUserId, cancellationToken);

        if (!userExists)
        {
            return new Failure<Guid>(
                command.PreviewId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.ApprovedByUserId), "User not found")]);
        }

        var preview = await _dataContext.MatchupPreviews
            .FirstOrDefaultAsync(x => x.Id == command.PreviewId, cancellationToken);

        if (preview is null)
        {
            return new Failure<Guid>(
                command.PreviewId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.PreviewId), "Preview not found.")]);
        }

        preview.ApprovedUtc = _dateTimeProvider.UtcNow();
        preview.ModifiedBy = command.ApprovedByUserId;

        // StatBot's pick follows the approved preview (MatchupPreviewApprovedConsumer).
        // Published BEFORE SaveChanges so the outbox captures it in the same
        // transaction as the approval. Sport comes from a league carrying the
        // contest; previews exist only for football today, so NCAA is the
        // fallback when no league has it yet.
        var sport = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.ContestId == preview.ContestId)
            .Join(_dataContext.PickemGroups.AsNoTracking(), m => m.GroupId, g => g.Id, (m, g) => (Sport?)g.Sport)
            .FirstOrDefaultAsync(cancellationToken) ?? Sport.FootballNcaa;

        await _eventBus.Publish(new MatchupPreviewApproved(
            MatchupPreviewId: preview.Id,
            ContestId: preview.ContestId,
            Ref: null,
            Sport: sport,
            SeasonYear: null,
            CorrelationId: Guid.NewGuid(),
            CausationId: CausationId.Api.PreviewApproval), cancellationToken);

        await _dataContext.SaveChangesAsync(cancellationToken);

        // IsPreviewReviewed lives in the cached league-week payload.
        await _cacheInvalidator.EvictForContestAsync(preview.ContestId, cancellationToken);

        return new Success<Guid>(preview.Id);
    }
}
