using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.User.Commands.UpdateDisplayName;

public interface IUpdateDisplayNameCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateDisplayNameCommand command,
        CancellationToken cancellationToken = default);
}

public class UpdateDisplayNameCommandHandler : IUpdateDisplayNameCommandHandler
{
    private readonly AppDataContext _db;
    private readonly ILogger<UpdateDisplayNameCommandHandler> _logger;
    private readonly IValidator<UpdateDisplayNameCommand> _validator;

    public UpdateDisplayNameCommandHandler(
        AppDataContext db,
        ILogger<UpdateDisplayNameCommandHandler> logger,
        IValidator<UpdateDisplayNameCommand> validator)
    {
        _db = db;
        _logger = logger;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        Guid userId,
        UpdateDisplayNameCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.BadRequest, validation.Errors);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(userId), $"User with ID {userId} not found.")]);
        }

        // Free-text label — trimmed and stored as-is (non-unique, unlike Username).
        user.DisplayName = command.DisplayName.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Display name updated. UserId={UserId}", user.Id);

        return new Success<Guid>(user.Id);
    }
}
