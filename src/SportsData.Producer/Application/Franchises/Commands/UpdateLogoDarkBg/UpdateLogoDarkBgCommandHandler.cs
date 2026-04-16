using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises.Commands.UpdateLogoDarkBg;

public interface IUpdateLogoDarkBgCommandHandler
{
    Task<Result<bool>> ExecuteAsync(UpdateLogoDarkBgCommand command, CancellationToken cancellationToken = default);
}

public class UpdateLogoDarkBgCommandHandler : IUpdateLogoDarkBgCommandHandler
{
    private readonly TeamSportDataContext _dbContext;

    public UpdateLogoDarkBgCommandHandler(TeamSportDataContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<bool>> ExecuteAsync(UpdateLogoDarkBgCommand command, CancellationToken cancellationToken = default)
    {
        if (command.LogoType == "franchise")
        {
            var logo = await _dbContext.FranchiseLogos
                .FirstOrDefaultAsync(l => l.Id == command.LogoId, cancellationToken);

            if (logo is null)
            {
                return new Failure<bool>(
                    false,
                    ResultStatus.NotFound,
                    [new FluentValidation.Results.ValidationFailure("LogoId", $"FranchiseLogo {command.LogoId} not found")]);
            }

            logo.IsForDarkBg = command.IsForDarkBg;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new Success<bool>(true);
        }

        if (command.LogoType == "franchiseSeason")
        {
            var logo = await _dbContext.FranchiseSeasonLogos
                .FirstOrDefaultAsync(l => l.Id == command.LogoId, cancellationToken);

            if (logo is null)
            {
                return new Failure<bool>(
                    false,
                    ResultStatus.NotFound,
                    [new FluentValidation.Results.ValidationFailure("LogoId", $"FranchiseSeasonLogo {command.LogoId} not found")]);
            }

            logo.IsForDarkBg = command.IsForDarkBg;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new Success<bool>(true);
        }

        return new Failure<bool>(
            false,
            ResultStatus.BadRequest,
            [new FluentValidation.Results.ValidationFailure("LogoType", $"Invalid LogoType: {command.LogoType}. Must be 'franchise' or 'franchiseSeason'")]);
    }
}
