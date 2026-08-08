using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Prompts;

/// <summary>
/// Edits a prompt's Text and/or Description. Name and slot
/// (Sport, WithStats) are deliberately immutable — a prompt's identity
/// never silently changes meaning; a different slot is a NEW version.
/// Captures store the exact text sent, so editing never rewrites history.
/// </summary>
public class UpdatePromptCommand
{
    public Guid PromptId { get; set; }

    public string? Description { get; set; }

    public required string Text { get; set; }
}

public class UpdatePromptCommandValidator : AbstractValidator<UpdatePromptCommand>
{
    public UpdatePromptCommandValidator()
    {
        RuleFor(x => x.PromptId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().WithMessage("Prompt text cannot be empty");
        RuleFor(x => x.Description).MaximumLength(256);
    }
}

public interface IUpdatePromptCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(UpdatePromptCommand command, CancellationToken cancellationToken);
}

public class UpdatePromptCommandHandler : IUpdatePromptCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdatePromptCommandHandler> _logger;

    public UpdatePromptCommandHandler(
        AppDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<UpdatePromptCommandHandler> logger)
    {
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(UpdatePromptCommand command, CancellationToken cancellationToken)
    {
        var validation = await new UpdatePromptCommandValidator().ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(default, ResultStatus.Validation, validation.Errors);
        }

        var prompt = await _dataContext.Prompts
            .FirstOrDefaultAsync(p => p.Id == command.PromptId, cancellationToken);

        if (prompt is null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.PromptId), "Prompt not found")]);
        }

        prompt.Text = command.Text.Replace("\r\n", "\n");
        prompt.Description = command.Description;
        prompt.ModifiedUtc = _dateTimeProvider.UtcNow();

        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Prompt updated. Id: {PromptId}, Name: {Name}, Length: {Length}",
            prompt.Id, prompt.Name, prompt.Text.Length);

        return new Success<Guid>(prompt.Id);
    }
}
