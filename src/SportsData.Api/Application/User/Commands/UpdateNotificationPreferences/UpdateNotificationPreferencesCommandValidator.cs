using FluentValidation;

namespace SportsData.Api.Application.User.Commands.UpdateNotificationPreferences;

/// <summary>
/// PickResultVoice must be a recognised wire name — an unknown voice stored
/// canonically would silently degrade to Standard copy at dispatch, so reject
/// it loudly at the API boundary instead. The bools remain structurally valid
/// by type.
/// </summary>
public class UpdateNotificationPreferencesCommandValidator
    : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleFor(x => x.PickResultVoice)
            .Must(SportsData.Core.Eventing.Events.Users.NotificationVoices.IsKnown)
            .WithMessage(x => $"Unknown PickResultVoice '{x.PickResultVoice}'.");
    }
}
