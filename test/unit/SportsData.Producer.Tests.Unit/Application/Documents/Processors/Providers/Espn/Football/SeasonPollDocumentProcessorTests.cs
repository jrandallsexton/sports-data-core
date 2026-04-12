using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

[Collection("Sequential")]
public class SeasonPollDocumentProcessorTests : ProducerTestBase<SeasonPollDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task ProcessAsync_ReturnsEarly_WhenDtoRefIsNull()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        // Craft a DTO with null Ref
        var dto = new EspnFootballSeasonRankingDto
        {
            Id = "1",
            Name = "AP Top 25",
            ShortName = "AP",
            Type = "ap"
        };
        // Ref is not set, defaults to null

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, dto.ToJson())
            .With(x => x.DocumentType, DocumentType.SeasonPoll)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.SeasonYear, 2025)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<SeasonPollDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert — no SeasonPoll created
        var polls = await FootballDataContext.SeasonPolls.ToListAsync();
        polls.Should().BeEmpty();
    }
}
