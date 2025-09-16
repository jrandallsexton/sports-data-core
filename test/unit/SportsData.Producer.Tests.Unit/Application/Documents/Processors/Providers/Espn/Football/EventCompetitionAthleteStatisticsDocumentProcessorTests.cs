using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    public class EventCompetitionAthleteStatisticsDocumentProcessorTests
        : ProducerTestBase<EventCompetitionAthleteStatisticsDocumentProcessor<TeamSportDataContext>>
    {
    }
}
