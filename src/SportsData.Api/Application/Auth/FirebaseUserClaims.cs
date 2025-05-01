namespace SportsData.Api.Application.Auth
{
    public class FirebaseUserClaims
    {
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public long AuthTime { get; set; }
        public string UserId { get; set; }
        public long IssuedAt { get; set; }
        public long Expiration { get; set; }
        public string Email { get; set; }
        public bool EmailVerified { get; set; }
        public FirebaseIdentityInfo Firebase { get; set; }
    }
}
