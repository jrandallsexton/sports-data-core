using FluentValidation;

namespace SportsData.Api.Application.User.Commands.UpdateUserOptions;

/// <summary>
/// Nothing to reject today (a bool is a bool), but the validator exists so
/// future options with real constraints (enums, ranges) slot into the same
/// pipeline every other command uses.
/// </summary>
public class UpdateUserOptionsCommandValidator : AbstractValidator<UpdateUserOptionsCommand>
{
}
