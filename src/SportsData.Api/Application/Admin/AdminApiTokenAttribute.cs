using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsData.Api.Application.Admin;

/// <summary>
/// Requires the X-Admin-Token header for authorization.
/// The token value must match the one configured in CommonConfig:Api:AdminToken
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminApiTokenAttribute : Attribute, IAuthorizationFilter
{
    /// <summary>
    /// Validates the admin API token from the request headers
    /// </summary>
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var validToken = configuration["CommonConfig:Api:AdminToken"];

        if (string.IsNullOrEmpty(validToken))
        {
            context.Result = new StatusCodeResult(500); // Server error if token not configured
            return;
        }

        var providedToken = context.HttpContext.Request.Headers["X-Admin-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(providedToken) || providedToken != validToken)
        {
            context.Result = new UnauthorizedResult();
        }
    }
}