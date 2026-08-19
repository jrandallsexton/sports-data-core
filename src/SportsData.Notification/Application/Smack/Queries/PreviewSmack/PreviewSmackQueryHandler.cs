#nullable enable

using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Smack.Queries.PreviewSmack;

public interface IPreviewSmackQueryHandler
{
    Task<Result<List<SmackPreviewResultDto>>> ExecuteAsync(
        SmackPreviewRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs the send path's exact resolution (situation → deterministic phrase →
/// formatting) for each pick WITHOUT dispatching. Fidelity is the contract:
/// allowGamblingContent derives from the pick's ATS-ness exactly as the
/// consumer does, so a rating grades precisely what a user would have
/// received.
/// </summary>
public class PreviewSmackQueryHandler : IPreviewSmackQueryHandler
{
    private readonly ISmackPhraseCatalog _catalog;

    public PreviewSmackQueryHandler(ISmackPhraseCatalog catalog)
    {
        _catalog = catalog;
    }

    public async Task<Result<List<SmackPreviewResultDto>>> ExecuteAsync(
        SmackPreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Picks is not { Count: > 0 })
        {
            return new Failure<List<SmackPreviewResultDto>>(
                [], ResultStatus.BadRequest,
                [new ValidationFailure(nameof(request.Picks), "At least one pick is required.")]);
        }

        if (request.Picks.Count > 500)
        {
            return new Failure<List<SmackPreviewResultDto>>(
                [], ResultStatus.BadRequest,
                [new ValidationFailure(nameof(request.Picks), "At most 500 picks per preview batch.")]);
        }

        // Wire string → enum with the same Standard fallback the preferences
        // projection applies. IsDefined guards the TryParse numeric loophole
        // ("999" parses to an undefined value).
        var voice = Enum.TryParse<NotificationVoice>(request.Voice, ignoreCase: false, out var parsed)
                    && Enum.IsDefined(parsed)
            ? parsed
            : NotificationVoice.Standard;

        var results = new List<SmackPreviewResultDto>(request.Picks.Count);
        foreach (var pick in request.Picks)
        {
            var msg = pick.ToEvent();

            // Same derivation as UserPickScoredConsumer: knowing the line and
            // being allowed to talk about it are different things.
            var allowGamblingContent = msg.PickedSpread is not null;

            var resolution = await _catalog.ResolveDetailedAsync(
                msg, voice, allowGamblingContent, cancellationToken);

            results.Add(new SmackPreviewResultDto(
                pick.PickId,
                resolution.Situation.ToString(),
                resolution.PhraseId,
                resolution.Text,
                resolution.UsedStandardFallback));
        }

        return new Success<List<SmackPreviewResultDto>>(results);
    }
}
