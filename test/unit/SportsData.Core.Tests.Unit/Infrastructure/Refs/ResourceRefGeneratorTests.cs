using FluentAssertions;

using Microsoft.Extensions.Configuration;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Infrastructure.Refs;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.Refs;

public class ResourceRefGeneratorTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IAppMode> _mockAppMode;
    private readonly ResourceRefGenerator _generator;

    public ResourceRefGeneratorTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockAppMode = new Mock<IAppMode>();
        
        // Set up default sport mode
        _mockAppMode.Setup(x => x.CurrentSport).Returns(Sport.FootballNcaa);
        
        // Set up configuration values (simulating Azure AppConfig)
        SetupConfiguration(
            producerUrl: "http://producer-svc-football-ncaa/api",
            contestUrl: "http://api-svc-football-ncaa/api",
            venueUrl: "http://venue-svc/api",
            franchiseUrl: "http://franchise-svc-football-ncaa/api"
        );
        
        _generator = new ResourceRefGenerator(_mockConfiguration.Object, _mockAppMode.Object);
    }

    private void SetupConfiguration(string producerUrl, string contestUrl, string venueUrl, string franchiseUrl)
    {
        _mockConfiguration
            .Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"])
            .Returns(producerUrl);
        
        _mockConfiguration
            .Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"])
            .Returns(contestUrl);
        
        _mockConfiguration
            .Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"])
            .Returns(venueUrl);
        
        _mockConfiguration
            .Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"])
            .Returns(franchiseUrl);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrow_WhenProducerUrlNotConfigured()
    {
        // Arrange
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"]).Returns((string)null);
        config.Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");

        // Act & Assert
        var act = () => new ResourceRefGenerator(config.Object, _mockAppMode.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ProducerClientConfig:ApiUrl*");
    }

    [Fact(Skip="Not ready for this implementation")]
    public void Constructor_ShouldThrow_WhenContestUrlNotConfigured()
    {
        // Arrange
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"]).Returns((string)null);
        config.Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");

        // Act & Assert
        var act = () => new ResourceRefGenerator(config.Object, _mockAppMode.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ContestClientConfig*FootballNcaa*");
    }

    [Fact(Skip = "Not ready for this implementation")]
    public void Constructor_ShouldThrow_WhenVenueUrlNotConfigured()
    {
        // Arrange
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"]).Returns((string)null);
        config.Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");

        // Act & Assert
        var act = () => new ResourceRefGenerator(config.Object, _mockAppMode.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*VenueClientConfig:ApiUrl*");
    }

    [Fact(Skip = "Not ready for this implementation")]
    public void Constructor_ShouldThrow_WhenFranchiseUrlNotConfigured()
    {
        // Arrange
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"]).Returns((string)null);

        // Act & Assert
        var act = () => new ResourceRefGenerator(config.Object, _mockAppMode.Object);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*FranchiseClientConfig*FootballNcaa*");
    }

    [Fact]
    public void Constructor_ShouldNotThrow_WhenAllConfigurationPresent()
    {
        // Arrange
        var config = new Mock<IConfiguration>();
        config.Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"]).Returns("http://test");
        config.Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"]).Returns("http://test");

        // Act & Assert
        var act = () => new ResourceRefGenerator(config.Object, _mockAppMode.Object);
        act.Should().NotThrow();
    }

    #endregion

    #region Producer Resource Tests

    [Fact]
    public void ForCompetition_ShouldGenerateCorrectUri()
    {
        // Arrange
        var competitionId = Guid.NewGuid();

        // Act
        var result = _generator.ForCompetition(competitionId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/competition/{competitionId}");
    }

    [Fact]
    public void ForAthlete_ShouldGenerateCorrectUri()
    {
        // Arrange
        var athleteId = Guid.NewGuid();

        // Act
        var result = _generator.ForAthlete(athleteId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/athlete/{athleteId}");
    }

    [Fact]
    public void ForSeason_ShouldGenerateCorrectUri()
    {
        // Arrange
        var seasonId = Guid.NewGuid();

        // Act
        var result = _generator.ForSeason(seasonId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/season/{seasonId}");
    }

    [Fact]
    public void ForCoach_ShouldGenerateCorrectUri()
    {
        // Arrange
        var coachId = Guid.NewGuid();

        // Act
        var result = _generator.ForCoach(coachId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/coach/{coachId}");
    }

    [Fact]
    public void ForSeasonPhase_ShouldGenerateCorrectUri()
    {
        // Arrange
        var seasonPhaseId = Guid.NewGuid();

        // Act
        var result = _generator.ForSeasonPhase(seasonPhaseId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/seasonphase/{seasonPhaseId}");
    }

    [Fact]
    public void ForSeasonWeek_ShouldGenerateCorrectUri()
    {
        // Arrange
        var seasonWeekId = Guid.NewGuid();

        // Act
        var result = _generator.ForSeasonWeek(seasonWeekId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/seasonweek/{seasonWeekId}");
    }

    #endregion

    #region Contest Resource Tests

    [Fact(Skip = "Not ready for this implementation")]
    public void ForContest_ShouldGenerateCorrectUri()
    {
        // Arrange
        var contestId = Guid.NewGuid();

        // Act
        var result = _generator.ForContest(contestId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://api-svc-football-ncaa/api/contest/{contestId}");
    }

    [Fact(Skip = "Not ready for this implementation")]
    public void ForPick_ShouldGenerateCorrectUri()
    {
        // Arrange
        var pickId = Guid.NewGuid();

        // Act
        var result = _generator.ForPick(pickId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://api-svc-football-ncaa/api/pick/{pickId}");
    }

    [Fact(Skip = "Not ready for this implementation")]
    public void ForRanking_ShouldGenerateCorrectUri()
    {
        // Arrange
        var seasonYear = 2024;

        // Act
        var result = _generator.ForRanking(seasonYear);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://api-svc-football-ncaa/api/rankings/{seasonYear}");
    }

    [Fact(Skip = "Not ready for this implementation")]
    public void ForMatchupPreview_ShouldGenerateCorrectUri()
    {
        // Arrange
        var contestId = Guid.NewGuid();

        // Act
        var result = _generator.ForMatchupPreview(contestId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://api-svc-football-ncaa/api/matchuppreview/{contestId}");
    }

    #endregion

    #region Venue Resource Tests

    [Fact(Skip = "Not ready for this implementation")]
    public void ForVenue_ShouldGenerateCorrectUri()
    {
        // Arrange
        var venueId = Guid.NewGuid();

        // Act
        var result = _generator.ForVenue(venueId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://venue-svc/api/venue/{venueId}");
    }

    #endregion

    #region Franchise Resource Tests

    [Fact(Skip = "Not ready for this implementation")]
    public void ForFranchise_ShouldGenerateCorrectUri()
    {
        // Arrange
        var franchiseId = Guid.NewGuid();

        // Act
        var result = _generator.ForFranchise(franchiseId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://franchise-svc-football-ncaa/api/franchise/{franchiseId}");
    }

    [Fact]
    public void ForFranchiseSeason_ShouldGenerateCorrectUri()
    {
        // Arrange
        var franchiseSeasonId = Guid.NewGuid();

        // Act
        var result = _generator.ForFranchiseSeason(franchiseSeasonId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/franchiseseason/{franchiseSeasonId}");
    }

    [Fact]
    public void ForAthleteSeason_ShouldGenerateCorrectUri()
    {
        // Arrange
        var athleteSeasonId = Guid.NewGuid();

        // Act
        var result = _generator.ForAthleteSeason(athleteSeasonId);

        // Assert
        result.Should().NotBeNull();
        result.AbsoluteUri.Should().Be($"http://producer-svc-football-ncaa/api/athleteseason/{athleteSeasonId}");
    }

    #endregion

    #region Environment-Specific Tests

    [Fact(Skip = "Not ready for this implementation")]
    public void ShouldGenerateDifferentUrls_ForDifferentEnvironments()
    {
        // Arrange - Setup for Kubernetes environment
        var k8sConfig = new Mock<IConfiguration>();
        k8sConfig.Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"]).Returns("http://producer-svc-football-ncaa");
        k8sConfig.Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"]).Returns("http://api-svc-football-ncaa");
        k8sConfig.Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"]).Returns("http://venue-svc");
        k8sConfig.Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"]).Returns("http://franchise-svc-football-ncaa");
        
        var k8sGenerator = new ResourceRefGenerator(k8sConfig.Object, _mockAppMode.Object);
        
        // Arrange - Setup for localhost environment
        var localConfig = new Mock<IConfiguration>();
        localConfig.Setup(x => x["CommonConfig:ProducerClientConfig:ApiUrl"]).Returns("http://localhost:5001");
        localConfig.Setup(x => x["CommonConfig:ContestClientConfig:FootballNcaa:ApiUrl"]).Returns("http://localhost:5002");
        localConfig.Setup(x => x["CommonConfig:VenueClientConfig:ApiUrl"]).Returns("http://localhost:5003");
        localConfig.Setup(x => x["CommonConfig:FranchiseClientConfig:FootballNcaa:ApiUrl"]).Returns("http://localhost:5004");
        
        var localGenerator = new ResourceRefGenerator(localConfig.Object, _mockAppMode.Object);
        
        var contestId = Guid.NewGuid();

        // Act
        var k8sUrl = k8sGenerator.ForContest(contestId);
        var localUrl = localGenerator.ForContest(contestId);

        // Assert
        k8sUrl.AbsoluteUri.Should().Contain("api-svc-football-ncaa");
        localUrl.AbsoluteUri.Should().Contain("localhost:5002");
        k8sUrl.Should().NotBe(localUrl);
    }

    #endregion

    #region URI Format Tests

    [Fact]
    public void GeneratedUris_ShouldBeAbsolute()
    {
        // Arrange & Act
        var competitionUri = _generator.ForCompetition(Guid.NewGuid());
        var contestUri = _generator.ForContest(Guid.NewGuid());
        var venueUri = _generator.ForVenue(Guid.NewGuid());

        // Assert
        competitionUri.IsAbsoluteUri.Should().BeTrue();
        contestUri.IsAbsoluteUri.Should().BeTrue();
        venueUri.IsAbsoluteUri.Should().BeTrue();
    }

    [Fact]
    public void GeneratedUris_ShouldUseLowercaseResourceNames()
    {
        // Arrange & Act
        var uri = _generator.ForCompetition(Guid.NewGuid());

        // Assert
        uri.AbsolutePath.Should().Contain("/api/competition/");
        uri.AbsolutePath.Should().NotContain("Competition"); // Ensure lowercase
    }

    #endregion
}
