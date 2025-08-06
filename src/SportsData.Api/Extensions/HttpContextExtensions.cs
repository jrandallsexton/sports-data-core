using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Extensions;

public static class HttpContextExtensions
{
    public static User GetCurrentUser(this HttpContext context)
    {
        if (context.Items.TryGetValue("User", out var userObj) && userObj is User user)
            return user;

        throw new UnauthorizedAccessException("Authenticated user not found in context.");
    }

    public static Guid GetCurrentUserId(this HttpContext context)
    {
        return context.GetCurrentUser().Id;
    }
}