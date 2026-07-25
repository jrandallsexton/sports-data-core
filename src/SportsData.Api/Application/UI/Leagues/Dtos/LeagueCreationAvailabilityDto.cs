namespace SportsData.Api.Application.UI.Leagues.Dtos;

/// <summary>
/// Which sports are currently gated from league creation. Only <b>active</b> gates
/// are listed (open instant still in the future); a sport not present is open. The
/// FE locks exactly the sports it sees here and shows "opens {date}".
/// </summary>
public record LeagueCreationAvailabilityDto(IReadOnlyList<LeagueCreationGateDto> Gates);

/// <summary>
/// A sport gated from league creation, with the UTC instant it opens. <see cref="Sport"/>
/// is the backend enum name (e.g. "FootballNcaa") the FE keys sports by.
/// </summary>
public record LeagueCreationGateDto(string Sport, DateTime OpensUtc);
