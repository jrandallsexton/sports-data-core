using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Auth
{
    public class FirebaseIdentityInfo
    {
        public Dictionary<string, string[]> Identities { get; set; }

        [JsonPropertyName("sign_in_provider")]
        public string SignInProvider { get; set; }
    }
}
