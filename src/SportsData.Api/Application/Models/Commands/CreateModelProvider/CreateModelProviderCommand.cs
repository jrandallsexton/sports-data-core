using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Models.Commands.CreateModelProvider;

public class CreateModelProviderCommand
{
    /// <summary>Display name (unique), e.g. "Anthropic".</summary>
    public required string Name { get; set; }

    /// <summary>Maps the row to its client implementation (DeepSeek/Anthropic/OpenAi/Google).</summary>
    public ModelProviderKind Kind { get; set; }

    public string? Description { get; set; }
}

public class CreateModelProviderCommandValidator : AbstractValidator<CreateModelProviderCommand>
{
    public CreateModelProviderCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(256);
    }
}

public interface ICreateModelProviderCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CreateModelProviderCommand command, CancellationToken cancellationToken);
}

public class CreateModelProviderCommandHandler : ICreateModelProviderCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<CreateModelProviderCommandHandler> _logger;

    public CreateModelProviderCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateModelProviderCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(CreateModelProviderCommand command, CancellationToken cancellationToken)
    {
        var validation = await new CreateModelProviderCommandValidator().ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default!, ResultStatus.Validation, validation.Errors);
        }

        var name = command.Name.Trim();

        var nameTaken = await _dataContext.ModelProviders
            .AsNoTracking()
            .AnyAsync(p => p.Name == name, cancellationToken);

        if (nameTaken)
        {
            return new Failure<Guid>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.Name), $"A provider named '{name}' already exists.")]);
        }

        var provider = new ModelProvider
        {
            Id = Guid.NewGuid(),
            Name = name,
            Kind = command.Kind,
            Description = command.Description,
            CreatedUtc = _dateTimeProvider.UtcNow()
        };

        await _dataContext.ModelProviders.AddAsync(provider, cancellationToken);
        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ModelProvider created. Id: {ProviderId}, Kind: {Kind}",
            provider.Id, provider.Kind);

        return new Success<Guid>(provider.Id);
    }
}
