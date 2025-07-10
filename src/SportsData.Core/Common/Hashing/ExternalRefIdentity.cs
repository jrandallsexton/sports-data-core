using System;
using SportsData.Core.Extensions;

namespace SportsData.Core.Common.Hashing
{
    public record ExternalRefIdentity(
        Guid CanonicalId,
        string UrlHash,
        string CleanUrl);

    public interface IGenerateExternalRefIdentities
    {
        ExternalRefIdentity Generate(Uri sourceUrl);
        ExternalRefIdentity Generate(string sourceUrl);
    }

    public class ExternalRefIdentityGenerator : IGenerateExternalRefIdentities
    {
        private ExternalRefIdentity GenerateInternal(Uri uri)
        {
            var urlHash = HashProvider.GenerateHashFromUri(uri);
            var canonicalId = DeterministicGuid.Combine(urlHash);
            var cleanUrl = uri.ToCleanUrl();

            return new ExternalRefIdentity(canonicalId, urlHash, cleanUrl);
        }

        public ExternalRefIdentity Generate(Uri sourceUrl)
        {
            if (sourceUrl == null)
                throw new ArgumentNullException(nameof(sourceUrl));

            return GenerateInternal(sourceUrl);
        }

        public ExternalRefIdentity Generate(string sourceUrl)
        {
            if (string.IsNullOrEmpty(sourceUrl))
                throw new ArgumentException("Source URL cannot be empty.", nameof(sourceUrl));

            var uri = new Uri(sourceUrl, UriKind.Absolute);
            return GenerateInternal(uri);
        }
    }
}
