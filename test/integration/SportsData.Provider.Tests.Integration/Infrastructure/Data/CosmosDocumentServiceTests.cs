using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.DependencyInjection;
using SportsData.Provider.Config;
using SportsData.Provider.Infrastructure.Data;

using Xunit;

namespace SportsData.Provider.Tests.Integration.Infrastructure.Data
{
    public class CosmosDocumentServiceTests
    {
        private readonly CosmosDocumentService _sut;
        private readonly string _containerName = "FootballNcaa"; // adjust if needed

        public CosmosDocumentServiceTests()
        {
            var config = new ProviderDocDatabaseConfig
            {
                ConnectionString = "connection_string_here",
                DatabaseName = "provider-dev"
            };

            var options = Options.Create(config);
            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<CosmosDocumentService>();
            
            // Use stub IAppMode implementation for testing
            var appMode = new TestAppMode(Sport.FootballNcaa);

            _sut = new CosmosDocumentService(logger, options, appMode);
        }
        
        // Stub implementation of IAppMode for testing
        private class TestAppMode : IAppMode
        {
            public TestAppMode(Sport sport)
            {
                CurrentSport = sport;
            }
            
            public Sport CurrentSport { get; }
        }

        [Fact(Skip = "cosmos troubleshooting locally only")]
        public async Task InsertAndRetrieve_DocumentBase_Succeeds()
        {
            // Arrange
            var testUri = new Uri("http://test.sportsdeets.com/foo/bar");
            var sourceHash = HashProvider.GenerateHashFromUri(testUri);
            var routingKey = sourceHash.Substring(0, 3).ToUpperInvariant();

            var doc = new DocumentBase
            {
                Id = sourceHash,
                Data = "{\"hello\": \"world\"}",
                Sport = Sport.FootballNcaa,
                DocumentType = DocumentType.Unknown,
                SourceDataProvider = SourceDataProvider.Espn,
                Uri = testUri,
                SourceUrlHash = sourceHash,
                RoutingKey = routingKey
            };

            // Act
            await _sut.InsertOneAsync(_containerName, doc);

            var retrieved = await _sut.GetFirstOrDefaultAsync<DocumentBase>(
                _containerName,
                x => x.Id == sourceHash);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved!.Id.Should().Be(doc.Id);
            retrieved.Data.Should().Be(doc.Data);
        }
    }
}
