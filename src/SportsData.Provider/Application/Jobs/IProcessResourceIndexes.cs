using Hangfire;

using SportsData.Provider.Application.Jobs.Definitions;

namespace SportsData.Provider.Application.Jobs;

public interface IProcessResourceIndexes
{
    // The DCE attribute MUST live here, not on ResourceIndexJob: jobs are
    // enqueued via Enqueue<IProcessResourceIndexes>(...), so Hangfire stores
    // the job against this interface method and resolves job filters from
    // it — attributes on the implementing class are never seen.
    [DisableConcurrentExecution(300)] // 5 minutes (outer gate)
    Task ExecuteAsync(DocumentJobDefinition jobDefinition);
}
