namespace SportsData.Provider.Application.Jobs;

public interface ISourcingJobOrchestrator
{
    Task ExecuteAsync();
}