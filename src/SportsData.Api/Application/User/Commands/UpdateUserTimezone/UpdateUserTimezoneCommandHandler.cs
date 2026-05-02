using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.User.Commands.UpdateUserTimezone;

public interface IUpdateUserTimezoneCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateUserTimezoneCommand command,
        CancellationToken cancellationToken = default);
}

public class UpdateUserTimezoneCommandHandler : IUpdateUserTimezoneCommandHandler
{
    private readonly AppDataContext _db;
    private readonly ILogger<UpdateUserTimezoneCommandHandler> _logger;
    private readonly IValidator<UpdateUserTimezoneCommand> _validator;

    public UpdateUserTimezoneCommandHandler(
        AppDataContext db,
        ILogger<UpdateUserTimezoneCommandHandler> logger,
        IValidator<UpdateUserTimezoneCommand> validator)
    {
        _db = db;
        _logger = logger;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateUserTimezoneCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default, ResultStatus.BadRequest, validation.Errors);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(userId), $"User with ID {userId} not found.")]);
        }

        var normalized = string.IsNullOrWhiteSpace(command.Timezone) ? null : command.Timezone.Trim();
        user.Timezone = normalized;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Timezone updated. UserId={UserId}, Timezone={Timezone}", user.Id, normalized);

        return new Success<Guid>(user.Id);
    }
}
