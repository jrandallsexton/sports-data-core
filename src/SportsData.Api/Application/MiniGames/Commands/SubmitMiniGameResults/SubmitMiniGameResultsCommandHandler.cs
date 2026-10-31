using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.MiniGames.Commands.SubmitMiniGameResults;

public interface ISubmitMiniGameResultsCommandHandler
{
    Task<Result<SubmitMiniGameResultsResponse>> ExecuteAsync(
        Guid userId,
        SubmitMiniGameResultsCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores an offline device's queued results. Idempotent on the
/// client-generated id: a re-upload of a stored result reports it as a
/// duplicate instead of inserting it again. An id already owned by another
/// user is rejected. No eventing: results are read back by the stats query
/// only. See docs/features/mini-game-results.md.
/// </summary>
public class SubmitMiniGameResultsCommandHandler : ISubmitMiniGameResultsCommandHandler
{
    private readonly AppDataContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<SubmitMiniGameResultsCommand> _validator;
    private readonly IValidator<MiniGameResultSubmission> _itemValidator;
    private readonly ILogger<SubmitMiniGameResultsCommandHandler> _logger;

    public SubmitMiniGameResultsCommandHandler(
        AppDataContext db,
        IDateTimeProvider clock,
        IValidator<SubmitMiniGameResultsCommand> validator,
        IValidator<MiniGameResultSubmission> itemValidator,
        ILogger<SubmitMiniGameResultsCommandHandler> logger)
    {
        _db = db;
        _clock = clock;
        _validator = validator;
        _itemValidator = itemValidator;
        _logger = logger;
    }

    public async Task<Result<SubmitMiniGameResultsResponse>> ExecuteAsync(
        Guid userId,
        SubmitMiniGameResultsCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<SubmitMiniGameResultsResponse>(default!, ResultStatus.BadRequest, validation.Errors);
        }

        var response = new SubmitMiniGameResultsResponse();
        var candidates = new List<MiniGameResultSubmission>();
        var seen = new HashSet<Guid>();

        foreach (var item in command.Results)
        {
            if (item.Id != Guid.Empty && !seen.Add(item.Id))
            {
                // Same id twice in one batch: the first copy wins.
                response.Duplicate.Add(item.Id);
                continue;
            }

            var itemValidation = await _itemValidator.ValidateAsync(item, cancellationToken);
            if (!itemValidation.IsValid)
            {
                _logger.LogWarning(
                    "Mini-game result rejected. UserId={UserId} ResultId={ResultId} Errors={Errors}",
                    userId, item.Id, string.Join("; ", itemValidation.Errors.Select(e => e.ErrorMessage)));
                response.Rejected.Add(item.Id);
                continue;
            }

            candidates.Add(item);
        }

        var toInsert = await ClassifyExistingAsync(userId, candidates, response, cancellationToken);
        var now = _clock.UtcNow();

        foreach (var item in toInsert)
        {
            await _db.MiniGameResults.AddAsync(ToEntity(userId, item, now), cancellationToken);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (toInsert.Count > 0 && ex.IsUniqueConstraintViolation())
        {
            // Race: a concurrent upload of the same queue (two flushes, or a
            // retry after a timed-out request that actually committed) stored
            // some of these ids between our read and SaveChanges. Detach ours,
            // re-classify against what's there now, and insert the remainder.
            _logger.LogWarning(ex,
                "Concurrent mini-game result upload. UserId={UserId}. Retrying.", userId);

            foreach (var entry in _db.ChangeTracker.Entries<MiniGameResult>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            toInsert = await ClassifyExistingAsync(userId, toInsert, response, cancellationToken);

            foreach (var item in toInsert)
            {
                await _db.MiniGameResults.AddAsync(ToEntity(userId, item, now), cancellationToken);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        response.Accepted.AddRange(toInsert.Select(i => i.Id));

        _logger.LogInformation(
            "Mini-game results submitted. UserId={UserId} Accepted={Accepted} Duplicate={Duplicate} Rejected={Rejected}",
            userId, response.Accepted.Count, response.Duplicate.Count, response.Rejected.Count);

        return new Success<SubmitMiniGameResultsResponse>(response);
    }

    /// <summary>
    /// Sorts candidates whose id is already stored into duplicate (this user's)
    /// or rejected (another user's), and returns the ones still to insert.
    /// </summary>
    private async Task<List<MiniGameResultSubmission>> ClassifyExistingAsync(
        Guid userId,
        List<MiniGameResultSubmission> candidates,
        SubmitMiniGameResultsResponse response,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
            return [];

        var ids = candidates.Select(c => c.Id).ToList();
        var owners = await _db.MiniGameResults
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.UserId })
            .ToDictionaryAsync(r => r.Id, r => r.UserId, cancellationToken);

        var remaining = new List<MiniGameResultSubmission>();
        foreach (var item in candidates)
        {
            if (!owners.TryGetValue(item.Id, out var ownerId))
            {
                remaining.Add(item);
            }
            else if (ownerId == userId)
            {
                response.Duplicate.Add(item.Id);
            }
            else
            {
                _logger.LogWarning(
                    "Mini-game result id belongs to another user. UserId={UserId} ResultId={ResultId}",
                    userId, item.Id);
                response.Rejected.Add(item.Id);
            }
        }

        return remaining;
    }

    private static MiniGameResult ToEntity(Guid userId, MiniGameResultSubmission item, DateTime now) => new()
    {
        Id = item.Id,
        UserId = userId,
        GameKey = item.GameKey,
        Outcome = item.Outcome,
        Score = item.Score,
        OpponentScore = item.OpponentScore,
        DurationSeconds = item.DurationSeconds,
        GameVersion = item.GameVersion,
        Seed = item.Seed,
        Detail = item.Detail?.GetRawText(),
        PlayedUtc = item.PlayedUtc.ToUniversalTime(),
        CreatedUtc = now,
        CreatedBy = userId
    };
}
