using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Application.Images.Processors.Responses;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Images.Processors.Responses;

public class FranchiseSeasonLogoResponseProcessorTests : ProducerTestBase<FranchiseSeasonLogoResponseProcessor<FootballDataContext>>
{
    [Fact]
    public async Task ProcessResponse_WhenFranchiseSeasonNotFound_LogsErrorAndReturns()
    {
        // arrange
        var sut = Mocker.CreateInstance<FranchiseSeasonLogoResponseProcessor<FootballDataContext>>();

        var response = new ProcessImageResponse(
            Uri: new Uri("https://cdn.example.com/logos/team-99.png"),
            ImageId: Guid.NewGuid().ToString(),
            OriginalUrlHash: "hash123",
            ParentEntityId: Guid.NewGuid(), // Non-existent FranchiseSeason
            Name: "team-99.png",
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2024,
            DocumentType: DocumentType.TeamSeason,
            SourceDataProvider: SourceDataProvider.Espn,
            Height: 500,
            Width: 500,
            Rel: ["full", "default"],
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());

        // act
        await sut.ProcessResponse(response);

        // assert
        var logoCount = await FootballDataContext.FranchiseSeasonLogos.CountAsync();
        Assert.Equal(0, logoCount);
    }

    [Fact]
    public async Task ProcessResponse_WhenLogoDoesNotExist_CreatesNewLogo()
    {
        // arrange
        var franchiseSeasonId = Guid.NewGuid();
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            Location = "Test City",
            Name = "Test Team",
            Slug = "test-team",
            ColorCodeHex = "#000000",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<FranchiseSeasonLogoResponseProcessor<FootballDataContext>>();

        var imageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var response = new ProcessImageResponse(
            Uri: new Uri("https://cdn.example.com/logos/team-99.png"),
            ImageId: imageId.ToString(),
            OriginalUrlHash: "hash123",
            ParentEntityId: franchiseSeasonId,
            Name: "team-99.png",
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2024,
            DocumentType: DocumentType.TeamSeason,
            SourceDataProvider: SourceDataProvider.Espn,
            Height: 500,
            Width: 500,
            Rel: ["full", "default"],
            CorrelationId: correlationId,
            CausationId: Guid.NewGuid());

        // act
        await sut.ProcessResponse(response);

        // assert
        var logo = await FootballDataContext.FranchiseSeasonLogos
            .FirstOrDefaultAsync(l => l.FranchiseSeasonId == franchiseSeasonId);

        Assert.NotNull(logo);
        Assert.Equal(imageId, logo.Id);
        Assert.Equal(franchiseSeasonId, logo.FranchiseSeasonId);
        Assert.Equal("https://cdn.example.com/logos/team-99.png", logo.Uri.ToString());
        Assert.Equal(500, logo.Height);
        Assert.Equal(500, logo.Width);
        Assert.Equal("hash123", logo.OriginalUrlHash);
        Assert.Equal(2, logo.Rel?.Count);
        Assert.Contains("full", logo.Rel);
        Assert.Contains("default", logo.Rel);
        Assert.Equal(correlationId, logo.CreatedBy);
    }

    [Fact]
    public async Task ProcessResponse_WhenLogoAlreadyExists_UpdatesExistingLogo()
    {
        // arrange
        var franchiseSeasonId = Guid.NewGuid();
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            Location = "Test City",
            Name = "Test Team",
            Slug = "test-team",
            ColorCodeHex = "#000000",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var existingLogoId = Guid.NewGuid();
        var existingLogo = new FranchiseSeasonLogo
        {
            Id = existingLogoId,
            FranchiseSeasonId = franchiseSeasonId,
            Uri = new Uri("https://cdn.example.com/logos/old-team-99.png"),
            Height = 300,
            Width = 300,
            OriginalUrlHash = "hash123",
            Rel = ["thumbnail"],
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.FranchiseSeasonLogos.AddAsync(existingLogo);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<FranchiseSeasonLogoResponseProcessor<FootballDataContext>>();

        var correlationId = Guid.NewGuid();
        var response = new ProcessImageResponse(
            Uri: new Uri("https://cdn.example.com/logos/new-team-99.png"), // Updated URI
            ImageId: existingLogoId.ToString(), // Use existing logo ID (canonical ID)
            OriginalUrlHash: "hash123", // Hash stays the same
            ParentEntityId: franchiseSeasonId,
            Name: "new-team-99.png",
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2024,
            DocumentType: DocumentType.TeamSeason,
            SourceDataProvider: SourceDataProvider.Espn,
            Height: 500, // Updated height
            Width: 500,  // Updated width
            Rel: ["full", "default"], // Updated rel
            CorrelationId: correlationId,
            CausationId: Guid.NewGuid());

        // act
        await sut.ProcessResponse(response);

        // assert
        var logoCount = await FootballDataContext.FranchiseSeasonLogos
            .CountAsync(l => l.FranchiseSeasonId == franchiseSeasonId);
        Assert.Equal(1, logoCount); // Should still be only 1 logo

        var logo = await FootballDataContext.FranchiseSeasonLogos
            .FirstOrDefaultAsync(l => l.FranchiseSeasonId == franchiseSeasonId);

        Assert.NotNull(logo);
        Assert.Equal(existingLogoId, logo.Id); // Should keep the original ID
        Assert.Equal(franchiseSeasonId, logo.FranchiseSeasonId);
        Assert.Equal("https://cdn.example.com/logos/new-team-99.png", logo.Uri.ToString()); // Updated
        Assert.Equal(500, logo.Height); // Updated
        Assert.Equal(500, logo.Width); // Updated
        Assert.Equal("hash123", logo.OriginalUrlHash); // Unchanged
        Assert.Equal(2, logo.Rel?.Count); // Updated
        Assert.Contains("full", logo.Rel);
        Assert.Contains("default", logo.Rel);
        Assert.Equal(correlationId, logo.ModifiedBy); // Should have ModifiedBy set
        Assert.NotNull(logo.ModifiedUtc); // Should have ModifiedUtc set
    }

    [Fact]
    public async Task ProcessResponse_WhenMultipleLogosExist_OnlyUpdatesMatchingId()
    {
        // arrange
        var franchiseSeasonId = Guid.NewGuid();
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            Location = "Test City",
            Name = "Test Team",
            Slug = "test-team",
            ColorCodeHex = "#000000",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var logo1Id = Guid.NewGuid();
        var logo1 = new FranchiseSeasonLogo
        {
            Id = logo1Id,
            FranchiseSeasonId = franchiseSeasonId,
            Uri = new Uri("https://cdn.example.com/logos/logo1.png"),
            Height = 300,
            Width = 300,
            OriginalUrlHash = "hash1",
            Rel = ["thumbnail"],
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        };

        var logo2Id = Guid.NewGuid();
        var logo2 = new FranchiseSeasonLogo
        {
            Id = logo2Id,
            FranchiseSeasonId = franchiseSeasonId,
            Uri = new Uri("https://cdn.example.com/logos/logo2.png"),
            Height = 500,
            Width = 500,
            OriginalUrlHash = "hash2",
            Rel = ["full"],
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.FranchiseSeasonLogos.AddAsync(logo1);
        await FootballDataContext.FranchiseSeasonLogos.AddAsync(logo2);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<FranchiseSeasonLogoResponseProcessor<FootballDataContext>>();

        var response = new ProcessImageResponse(
            Uri: new Uri("https://cdn.example.com/logos/updated-logo1.png"),
            ImageId: logo1Id.ToString(), // Update logo1 using its canonical ID
            OriginalUrlHash: "hash1",
            ParentEntityId: franchiseSeasonId,
            Name: "updated-logo1.png",
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2024,
            DocumentType: DocumentType.TeamSeason,
            SourceDataProvider: SourceDataProvider.Espn,
            Height: 400,
            Width: 400,
            Rel: ["medium"],
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());

        // act
        await sut.ProcessResponse(response);

        // assert
        var logoCount = await FootballDataContext.FranchiseSeasonLogos
            .CountAsync(l => l.FranchiseSeasonId == franchiseSeasonId);
        Assert.Equal(2, logoCount); // Should still be 2 logos

        var updatedLogo = await FootballDataContext.FranchiseSeasonLogos
            .FirstOrDefaultAsync(l => l.Id == logo1Id);
        Assert.NotNull(updatedLogo);
        Assert.Equal("https://cdn.example.com/logos/updated-logo1.png", updatedLogo.Uri.ToString());
        Assert.Equal(400, updatedLogo.Height);

        var unchangedLogo = await FootballDataContext.FranchiseSeasonLogos
            .FirstOrDefaultAsync(l => l.Id == logo2Id);
        Assert.NotNull(unchangedLogo);
        Assert.Equal("https://cdn.example.com/logos/logo2.png", unchangedLogo.Uri.ToString());
        Assert.Equal(500, unchangedLogo.Height);
    }
}
