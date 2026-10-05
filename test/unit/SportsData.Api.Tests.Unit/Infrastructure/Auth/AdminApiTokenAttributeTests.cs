using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Api.Infrastructure.Auth;

using System.Collections.Generic;
using System.Security.Claims;

using Xunit;

namespace SportsData.Api.Tests.Unit.Infrastructure.Auth;

/// <summary>
/// [AdminApiToken] is the only thing standing between every admin endpoint
/// and the public internet: it admits the shared X-Admin-Token header OR an
/// authenticated user carrying the Admin role claim, and nothing else.
/// </summary>
public class AdminApiTokenAttributeTests
{
    private const string ValidToken = "the-admin-token";

    [Fact]
    public void ValidHeaderToken_IsAdmitted()
    {
        var context = BuildContext(headerToken: ValidToken, user: Anonymous());

        new AdminApiTokenAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void AuthenticatedUserWithAdminRole_IsAdmitted()
    {
        var context = BuildContext(headerToken: null, user: Authenticated(isAdmin: true));

        new AdminApiTokenAttribute().OnAuthorization(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public void AuthenticatedUserWithoutAdminRole_IsForbidden()
    {
        // Known caller, wrong role: 403. Re-authenticating would not help.
        var context = BuildContext(headerToken: null, user: Authenticated(isAdmin: false));

        new AdminApiTokenAttribute().OnAuthorization(context);

        context.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void Anonymous_WithWrongOrMissingToken_IsUnauthorized()
    {
        var wrong = BuildContext(headerToken: "not-the-token", user: Anonymous());
        var missing = BuildContext(headerToken: null, user: Anonymous());

        new AdminApiTokenAttribute().OnAuthorization(wrong);
        new AdminApiTokenAttribute().OnAuthorization(missing);

        wrong.Result.Should().BeOfType<UnauthorizedResult>();
        missing.Result.Should().BeOfType<UnauthorizedResult>();
    }

    private static AuthorizationFilterContext BuildContext(string? headerToken, ClaimsPrincipal user)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommonConfig:Api:AdminToken"] = ValidToken
            })
            .Build();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton<IConfiguration>(configuration)
                .BuildServiceProvider(),
            User = user
        };

        if (headerToken is not null)
        {
            httpContext.Request.Headers["X-Admin-Token"] = headerToken;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal Authenticated(bool isAdmin)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "firebase-uid") };
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Firebase"));
    }
}
