namespace SportsData.Producer.Application.Franchises.Commands.UpdateLogoDarkBg;

public record UpdateLogoDarkBgCommand(Guid LogoId, bool IsForDarkBg, string LogoType);
