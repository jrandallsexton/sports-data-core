using FluentValidation;

namespace SportsData.Api.Application.User.Commands.UpdateUserTimezone;

public class UpdateUserTimezoneCommandValidator : AbstractValidator<UpdateUserTimezoneCommand>
{
    public UpdateUserTimezoneCommandValidator()
    {
        RuleFor(x => x.Timezone)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x.Timezone))
            .WithMessage("Timezone must not exceed 100 characters.");

        // Accept any IANA zone the runtime can resolve. .NET 8+ ships ICU/tzdata
        // cross-platform, so "America/New_York", "Europe/London", "Asia/Tokyo"
        // all parse on Linux and Windows.
        RuleFor(x => x.Timezone)
            .Must(BeAValidTimezone)
            .When(x => !string.IsNullOrWhiteSpace(x.Timezone))
            .WithMessage("Timezone must be a valid IANA timezone identifier.");
    }

    private static bool BeAValidTimezone(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz))
            return true;

        return TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _);
    }
}
