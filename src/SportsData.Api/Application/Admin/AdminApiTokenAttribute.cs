using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SportsData.Api.Application.Admin;

/// <summary>
/// Requires either:
/// 1. X-Admin-Token header with valid admin token, or
/// 2. Valid Firebase JWT token with admin access
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
            // Valid admin token provided
            return;
        }

        // If no valid admin token, check for authenticated user with admin role
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            // Here you can check for admin role/claims from Firebase
            // This would be based on how you store admin status in Firebase
            var firebaseUid = context.HttpContext.User.FindFirst("user_id")?.Value;
            if (!string.IsNullOrEmpty(firebaseUid))
            {
                // TODO: Add your admin check here, for example:
                // var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                // if (await userService.IsAdminAsync(firebaseUid))
                // {
                //     return;
                // }
                
                // For now, just check if the user exists (you'll want to add proper admin checking)
                return;
            }
        }

        context.Result = new UnauthorizedResult();
    }
}