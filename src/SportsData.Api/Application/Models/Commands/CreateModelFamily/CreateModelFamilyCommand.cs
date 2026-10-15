using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Commands.CreateModelFamily;

public class CreateModelFamilyCommand
{
    /// <summary>Display name (unique), e.g. "Qwen".</summary>
    public required string Name { get; set; }

    public string? Description { get; set; }
}

public class CreateModelFamilyCommandValidator : AbstractValidator<CreateModelFamilyCommand>
{
    public CreateModelFamilyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(512);
    }
}

public interface ICreateModelFamilyCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CreateModelFamilyCommand command, CancellationToken cancellationToken);
}

public class CreateModelFamilyCommandHandler : ICreateModelFamilyCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateModelFamilyCommandHandler> _logger;

    public CreateModelFamilyCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateModelFamilyCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateModelFamilyCommand command, CancellationToken cancellationToken)
    {
        var validation = await new CreateModelFamilyCommandValidator().ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);
        }

        var name = command.Name.Trim();

        var nameTaken = await _dataContext.ModelFamilies
            .AsNoTracking()
            .AnyAsync(f => f.Name == name, cancellationToken);

        if (nameTaken)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Name), $"A family named '{name}' already exists.")]);
        }

        var family = new ModelFamily
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = command.Description,
            CreatedUtc = _dateTimeProvider.UtcNow()
        };

        await _dataContext.ModelFamilies.AddAsync(family, cancellationToken);

        try
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // A concurrent create with the same name passed the check above;
            // the unique name index rejects the loser. Same 400 as that check.
            _logger.LogWarning(ex, "Concurrent ModelFamily create rejected (duplicate name).");
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Name), $"A family named '{name}' already exists.")]);
        }

        _logger.LogInformation("ModelFamily created. Id: {FamilyId}", family.Id);

        return new Success<Guid>(family.Id);
    }
}
