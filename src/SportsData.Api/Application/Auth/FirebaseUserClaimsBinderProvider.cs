using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SportsData.Api.Application.Auth
{
    public class FirebaseUserClaimsBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context.Metadata.ModelType == typeof(FirebaseUserClaims))
            {
                return new FirebaseUserClaimsBinder();
            }

            return null!;
        }
    }

}
