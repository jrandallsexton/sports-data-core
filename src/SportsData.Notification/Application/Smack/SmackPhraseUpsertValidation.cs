#nullable enable

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Smack;

/// <summary>
/// Shared parse-and-validate for phrase create/update — one definition so the
/// two commands cannot drift. Returns an error message, or null with the
/// parsed enum values on success.
/// </summary>
internal static class SmackPhraseUpsertValidation
{
    public static string? Validate(
        SmackPhraseUpsertDto? request,
        out NotificationVoice voice,
        out PickSituation situation,
        out Sport? sport)
    {
        voice = default;
        situation = default;
        sport = null;

        if (request is null)
            return "A phrase payload is required.";

        if (string.IsNullOrWhiteSpace(request.Text))
            return "Text is required.";

        if (request.Text.Length > 300)
            return "Text must be 300 characters or fewer.";

        if (request.Description is { Length: > 256 })
            return "Description must be 256 characters or fewer.";

        if (request.Weight < 1)
            return "Weight must be at least 1.";

        // IsDefined on every parse: TryParse accepts numeric strings like
        // "999", which would persist undefined enum values.
        if (!Enum.TryParse(request.Voice, ignoreCase: false, out voice) || !Enum.IsDefined(voice))
            return $"Unknown voice '{request.Voice}'.";

        if (!Enum.TryParse(request.Situation, ignoreCase: false, out situation) || !Enum.IsDefined(situation))
            return $"Unknown situation '{request.Situation}'.";

        if (request.Sport is not null)
        {
            if (!Enum.TryParse<Sport>(request.Sport, ignoreCase: false, out var parsedSport)
                || !Enum.IsDefined(parsedSport))
                return $"Unknown sport '{request.Sport}'.";
            sport = parsedSport;
        }

        return null;
    }

    public static SmackPhraseDto ToDto(SmackPhrase p) => new(
        p.Id, p.Voice.ToString(), p.Situation.ToString(), p.Sport?.ToString(),
        p.Text, p.IsActive, p.RequiresGamblingContent, p.Weight, p.Description,
        p.RowVersion);
}
