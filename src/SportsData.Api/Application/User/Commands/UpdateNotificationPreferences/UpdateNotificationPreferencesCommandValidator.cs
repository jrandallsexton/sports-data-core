using FluentValidation;

namespace SportsData.Api.Application.User.Commands.UpdateNotificationPreferences;

/// <summary>
/// No field-level rules today — every property is a non-nullable bool, so any
/// deserialized command is structurally valid. Kept (and auto-registered) so the
/// handler's <c>IValidator&lt;T&gt;</c> dependency resolves and there's a home for
/// future cross-field rules.
/// </summary>
public class UpdateNotificationPreferencesCommandValidator
    : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
    }
}
