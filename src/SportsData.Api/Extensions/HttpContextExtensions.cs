using Microsoft.AspNetCore.Mvc;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Extensions;

public static class HttpContextExtensions
{
    public static User GetCurrentUser(this HttpContext context)
    {
        if (context.Items.TryGetValue("User", out var userObj) && userObj is User user)
            return user;

        // Defensive logging - helps diagnose auth middleware issues
        var logger = context.RequestServices.GetRequiredService<ILogger<HttpContext>>();
        logger.LogError(
            "User not found in HttpContext.Items. Path: {Path}, IsAuthenticated: {IsAuthenticated}, UserId: {UserId}",
            context.Request.Path,
            context.User?.Identity?.IsAuthenticated ?? false,
            context.User?.FindFirst("user_id")?.Value ?? "null"
        );

        throw new UnauthorizedAccessException("Authenticated user not found in context.");
    }

    public static Guid GetCurrentUserId(this HttpContext context)
    {
        return context.GetCurrentUser().Id;
    }
}