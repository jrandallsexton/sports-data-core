using System;
using System.Linq;

namespace SportsData.Core.Infrastructure.DataSources.Espn
{
    public static class EspnRequestUri
    {
        // Only upgrade these domains; keep identity/caching unchanged
        private static readonly string[] HttpsDomains = { ".espn.com", ".go.com" };

        public static Uri ForFetch(Uri identityUri)
        {
            if (identityUri is null) throw new ArgumentNullException(nameof(identityUri));
            if (!identityUri.IsAbsoluteUri) return identityUri; // leave relative alone

            if (identityUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && IsTargetDomain(identityUri.Host))
            {
                var b = new UriBuilder(identityUri)
                {
                    Scheme = Uri.UriSchemeHttps,
                    Port = -1 // clear :80
                };
                return b.Uri;
            }

            return identityUri;
        }

        private static bool IsTargetDomain(string host) =>
            HttpsDomains.Any(d => host.EndsWith(d, StringComparison.OrdinalIgnoreCase));
    }
}