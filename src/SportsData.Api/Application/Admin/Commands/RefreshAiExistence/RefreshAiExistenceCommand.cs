namespace SportsData.Api.Application.Admin.Commands.RefreshAiExistence;

public class RefreshAiExistenceCommand
{
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Week to (re)fill. Null = the current week. Any week of the current
    /// season can be named so a missed week can be backfilled after the fact.
    /// </summary>
    public int? Week { get; set; }
}
