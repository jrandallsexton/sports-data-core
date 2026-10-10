using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis.Lab;

public interface IRatePlayAnalysisCommandHandler
{
    Task<Result<PlayAnalysisRatingDto>> ExecuteAsync(
        Guid playAnalysisId,
        int stars,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores an admin's 0-4 star rating of one fact (upsert; one rating per
/// fact). A racing first rating that loses on the unique index updates the
/// winner's row instead, as SmackBot's rating handler does.
/// </summary>
public class RatePlayAnalysisCommandHandler : IRatePlayAnalysisCommandHandler
{
    private readonly FootballDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RatePlayAnalysisCommandHandler(FootballDataContext dataContext, IDateTimeProvider dateTimeProvider)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<PlayAnalysisRatingDto>> ExecuteAsync(
        Guid playAnalysisId,
        int stars,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        if (stars is < 0 or > 4)
        {
            return new Failure<PlayAnalysisRatingDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(stars), "Stars must be between 0 and 4.")]);
        }

        var factExists = await _dataContext.PlayAnalyses
            .AsNoTracking()
            .AnyAsync(x => x.Id == playAnalysisId, cancellationToken);

        if (!factExists)
        {
            return new Failure<PlayAnalysisRatingDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(playAnalysisId), "Fact not found.")]);
        }

        var now = _dateTimeProvider.UtcNow();

        var existing = await _dataContext.PlayAnalysisRatings
            .FirstOrDefaultAsync(x => x.PlayAnalysisId == playAnalysisId, cancellationToken);

        if (existing is null)
        {
            var rating = new PlayAnalysisRating
            {
                PlayAnalysisId = playAnalysisId,
                Stars = stars,
                CreatedUtc = now,
                CreatedBy = correlationId
            };
            _dataContext.PlayAnalysisRatings.Add(rating);

            try
            {
                await _dataContext.SaveChangesAsync(cancellationToken);
                return new Success<PlayAnalysisRatingDto>(new PlayAnalysisRatingDto(playAnalysisId, stars));
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                _dataContext.Entry(rating).State = EntityState.Detached;
                existing = await _dataContext.PlayAnalysisRatings
                    .FirstAsync(x => x.PlayAnalysisId == playAnalysisId, cancellationToken);
            }
        }

        existing.Stars = stars;
        existing.ModifiedUtc = now;
        existing.ModifiedBy = correlationId;
        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<PlayAnalysisRatingDto>(new PlayAnalysisRatingDto(playAnalysisId, stars));
    }
}
