using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;

namespace SportsData.Api.Application.UI.Conferences
{
    public interface IConferenceService
    {
        Task<List<ConferenceDivisionNameAndSlugDto>> GetConferenceNamesAndSlugs(
            CancellationToken cancellationToken = default);
    }

    public class ConferenceService : IConferenceService
    {
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public ConferenceService(
            IProvideCanonicalData canonicalDataProvider)
        {
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task<List<ConferenceDivisionNameAndSlugDto>> GetConferenceNamesAndSlugs(
            CancellationToken cancellationToken = default)
        {
            return await _canonicalDataProvider.GetConferenceNamesAndSlugsForSeasonYear(2025); // TODO: Replace with dynamic year
        }
    }
}
