using Microsoft.AspNetCore.Mvc.ModelBinding;

using System.Security.Claims;
using System.Text.Json;

namespace SportsData.Api.Application.Auth
{
    public class FirebaseUserClaimsBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var user = bindingContext.HttpContext.User;

            var claimsDict = user.Claims.ToDictionary(c => c.Type, c => c.Value);

            var model = new FirebaseUserClaims
            {
                Issuer = claimsDict.GetValueOrDefault("iss"),
                Audience = claimsDict.GetValueOrDefault("aud"),
                AuthTime = long.TryParse(claimsDict.GetValueOrDefault("auth_time"), out var authTime) ? authTime : 0,
                UserId = claimsDict.GetValueOrDefault("user_id"),
                IssuedAt = long.TryParse(claimsDict.GetValueOrDefault("iat"), out var iat) ? iat : 0,
                Expiration = long.TryParse(claimsDict.GetValueOrDefault("exp"), out var exp) ? exp : 0,
                Email = claimsDict.GetValueOrDefault(ClaimTypes.Email) ?? claimsDict.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"),
                EmailVerified = bool.TryParse(claimsDict.GetValueOrDefault("email_verified"), out var verified) && verified,
                Firebase = JsonSerializer.Deserialize<FirebaseIdentityInfo>(
                    claimsDict.GetValueOrDefault("firebase") ?? "{}",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                )
            };

            bindingContext.Result = ModelBindingResult.Success(model);
            return Task.CompletedTask;
        }
    }
}
