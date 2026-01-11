using SportsData.Core.Infrastructure.Clients.Contest.Queries;

namespace SportsData.Core.Infrastructure.Clients.Contest.Queries;

public record GetContestByIdResponse(SeasonContestDto Contest);
