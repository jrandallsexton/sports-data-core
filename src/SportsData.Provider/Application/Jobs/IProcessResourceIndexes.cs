using SportsData.Provider.Application.Jobs.Definitions;

namespace SportsData.Provider.Application.Jobs;

public interface IProcessResourceIndexes
{
    Task ExecuteAsync(DocumentJobDefinition jobDefinition);
}