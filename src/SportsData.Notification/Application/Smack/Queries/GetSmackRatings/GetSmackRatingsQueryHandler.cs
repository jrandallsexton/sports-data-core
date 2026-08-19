#nullable enable

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Smack.Queries.GetSmackRatings;

public interface IGetSmackRatingsQueryHandler
{
    Task<Result<List<SmackRatingDto>>> ExecuteAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stored ratings for a league, so the Lab re-hydrates stars on reload. The
/// client matches on (PickId, Voice) AND RenderedText — a rating graded a
/// specific line, and must not display against a phrase that has since been
/// edited.
/// </summary>
public class GetSmackRatingsQueryHandler : IGetSmackRatingsQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetSmackRatingsQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<SmackRatingDto>>> ExecuteAsync(
        Guid leagueId,
        CancellationToken cancellationToken = default)
    {
        if (leagueId == Guid.Empty)
        {
            return new Failure<List<SmackRatingDto>>(
                [], ResultStatus.BadRequest,
                [new ValidationFailure(nameof(leagueId), "leagueId is required.")]);
        }

        var ratings = await _dataContext.SmackPreviewRatings
            .AsNoTracking()
            .Where(r => r.LeagueId == leagueId)
            .Select(r => new SmackRatingDto(
                r.PickId,
                r.Voice.ToString(),
                r.Situation.ToString(),
                r.PhraseId,
                r.RenderedText,
                r.Stars))
            .ToListAsync(cancellationToken);

        return new Success<List<SmackRatingDto>>(ratings);
    }
}
