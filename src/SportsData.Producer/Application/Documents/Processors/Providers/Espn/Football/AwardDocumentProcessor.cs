using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Award)]
public class AwardDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : BaseDataContext
{
    private readonly ILogger<AwardDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;

    public AwardDocumentProcessor(
        ILogger<AwardDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        _logger.LogInformation("Began with {Command}", command);
        // TODO: Implement processing logic
        await Task.Delay(100);
    }
}