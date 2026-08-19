#nullable enable

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Smack.Commands.RateSmackPreview;

public interface IRateSmackPreviewCommandHandler
{
    Task<Result<bool>> ExecuteAsync(
        SmackRatingRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Upserts on (PickId, Voice): re-rating after a phrase edit overwrites
/// rather than duplicates, so the training set holds one current opinion per
/// previewed pick.
/// </summary>
public class RateSmackPreviewCommandHandler : IRateSmackPreviewCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public RateSmackPreviewCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<bool>> ExecuteAsync(
        SmackRatingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return Invalid("A rating payload is required.");

        if (request.Stars is < 0 or > 4)
            return Invalid("Stars must be between 0 and 4.");

        if (string.IsNullOrWhiteSpace(request.RenderedText))
            return Invalid("RenderedText is required — it is the string being rated.");

        if (request.RenderedText.Length > 400)
            return Invalid("RenderedText must be 400 characters or fewer.");

        // Ratings are training rows — a mislabelled voice or situation poisons
        // the set, so invalid values reject rather than fall back. IsDefined
        // guards the TryParse numeric loophole.
        if (!Enum.TryParse<NotificationVoice>(request.Voice, ignoreCase: false, out var voice)
            || !Enum.IsDefined(voice))
            return Invalid($"Unknown voice '{request.Voice}'.");

        if (!Enum.TryParse<PickSituation>(request.Situation, ignoreCase: false, out var situation)
            || !Enum.IsDefined(situation))
            return Invalid($"Unknown situation '{request.Situation}'.");

        var now = _dateTimeProvider.UtcNow();

        var existing = await _dataContext.SmackPreviewRatings
            .FirstOrDefaultAsync(r => r.PickId == request.PickId && r.Voice == voice, cancellationToken);

        if (existing is null)
        {
            _dataContext.SmackPreviewRatings.Add(new SmackPreviewRating
            {
                Id = Guid.NewGuid(),
                PickId = request.PickId,
                ContestId = request.ContestId,
                LeagueId = request.LeagueId,
                PickerUserId = request.PickerUserId,
                Voice = voice,
                Situation = situation,
                PhraseId = request.PhraseId,
                RenderedText = request.RenderedText,
                Stars = request.Stars,
                FactsJson = request.FactsJson ?? "{}",
                CreatedUtc = now,
                CreatedBy = Guid.Empty
            });
        }
        else
        {
            existing.Situation = situation;
            existing.PhraseId = request.PhraseId;
            existing.RenderedText = request.RenderedText;
            existing.Stars = request.Stars;
            existing.FactsJson = request.FactsJson ?? existing.FactsJson;
            existing.ModifiedUtc = now;
        }

        try
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race between two concurrent first-time ratings of the same pick —
            // vanishingly rare on an admin surface, and the loser's opinion is
            // equally valid: re-run as an update.
            _dataContext.ChangeTracker.Clear();
            var winner = await _dataContext.SmackPreviewRatings
                .FirstAsync(r => r.PickId == request.PickId && r.Voice == voice, cancellationToken);
            winner.Situation = situation;
            winner.PhraseId = request.PhraseId;
            winner.RenderedText = request.RenderedText;
            winner.Stars = request.Stars;
            winner.FactsJson = request.FactsJson ?? winner.FactsJson;
            winner.ModifiedUtc = now;
            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        return new Success<bool>(true);
    }

    private static Failure<bool> Invalid(string message) => new(
        false, ResultStatus.BadRequest,
        [new ValidationFailure(nameof(SmackRatingRequestDto), message)]);

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
}
