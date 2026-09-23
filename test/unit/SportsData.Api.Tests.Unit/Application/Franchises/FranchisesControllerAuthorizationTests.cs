using System.Reflection;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Franchises;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Franchises;

/// <summary>
/// The enrich action is the only write on the franchises controller and it
/// fans out Producer work; it must be admin-gated. Pinned by reflection so a
/// refactor that drops the attribute fails a test, not a production check.
/// </summary>
public class FranchisesControllerAuthorizationTests
{
    [Fact]
    public void EnrichFranchiseSeason_IsAdminGated_AndIsAPost()
    {
        var action = typeof(FranchisesController).GetMethod(nameof(FranchisesController.EnrichFranchiseSeason));

        action.Should().NotBeNull();
        action!.GetCustomAttribute<AdminApiTokenAttribute>().Should().NotBeNull(
            "enrichment fans out Producer work and must require the Admin role or the ops token");
        action.GetCustomAttribute<HttpPostAttribute>().Should().NotBeNull();
        action.GetCustomAttribute<HttpPostAttribute>()!.Template
            .Should().Be("{franchiseIdOrSlug}/seasons/{seasonYear}/enrich");
    }

    [Fact]
    public void ReadActions_AreNotAdminGated()
    {
        // Guard against the attribute drifting onto the class and locking
        // public reads behind admin.
        typeof(FranchisesController).GetCustomAttribute<AdminApiTokenAttribute>().Should().BeNull();
        typeof(FranchisesController).GetMethod(nameof(FranchisesController.GetFranchiseSeasonById))!
            .GetCustomAttribute<AdminApiTokenAttribute>().Should().BeNull();
    }
}
