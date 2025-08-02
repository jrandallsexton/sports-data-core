using System.Threading.Tasks;
using Xunit;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

public class TeamSeasonInjuriesDocumentProcessorTests : ProducerTestBase<TeamSeasonInjuriesDocumentProcessor<TeamSportDataContext>>
{
    [Fact(Skip = "TBD")]
    public async Task NotYetImplemented_Fails()
    {
        await Task.Delay(100);
        Assert.Fail("Test not yet implemented for TeamSeasonInjuriesDocumentProcessor");
    }
}
