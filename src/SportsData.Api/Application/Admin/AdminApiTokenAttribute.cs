using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace SportsData.Api.Application.Admin;

/// <summary>
/// Requires either:
/// 1. X-Admin-Token header with valid admin token, or
/// 2. Valid Firebase JWT token with Admin role claim
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminApiTokenAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // First check for admin token in header
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var validToken = configuration["CommonConfig:Api:AdminToken"];

        var providedToken = context.HttpContext.Request.Headers["X-Admin-Token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(providedToken) && !string.IsNullOrEmpty(validToken) && providedToken == validToken)
        {
            // Valid admin token provided - bypass user auth check
            return;
        }

        // If no valid admin token, check for authenticated user with Admin role
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            // Check claims directly for Admin role
            var hasAdminRole = context.HttpContext.User.Claims
                .Any(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
            
            if (hasAdminRole)
            {
                return;
            }
        }

        // Neither admin token nor admin role found
        context.Result = new UnauthorizedResult();
    }
}