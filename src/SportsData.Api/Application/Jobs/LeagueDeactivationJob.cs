using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Nightly job that soft-closes finished leagues by stamping
    /// <see cref="Infrastructure.Data.Entities.PickemGroup.DeactivatedUtc"/>.
    /// Deactivation is not deletion: ~8 read sites (e.g. <c>/user/me</c>,
    /// GetUserLeagues, pick-import, MatchupScheduler) filter active-only on this
    /// field, so a stamped league drops out of the active surface but remains
    /// viewable via the "show past leagues" (<c>IncludeDeactivated</c>) toggle.
    ///
    /// Rule 1 (only rule today): a league whose window has an explicit
    /// <see cref="Infrastructure.Data.Entities.PickemGroup.EndsOn"/> is
    /// deactivated once at least <see cref="DeactivationGraceDays"/> days have
    /// elapsed since that end datetime. Leagues with a null <c>EndsOn</c>
    /// (season-long) are out of scope here and left for a future rule.
    ///
    /// The job only ever stamps null → value; it never reactivates. Extending a
    /// league's <c>EndsOn</c> after deactivation isn't exposed today, so there's
    /// nothing to un-stamp.
    /// </summary>
    public class LeagueDeactivationJob : IAmARecurringJob
    {
        /// <summary>
        /// Days that must elapse after a league's <c>EndsOn</c> before it is
        /// deactivated. Gives just-finished leagues a grace window before they
        /// slide into history.
        /// </summary>
        private const int DeactivationGraceDays = 7;

        private readonly ILogger<LeagueDeactivationJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public LeagueDeactivationJob(
            ILogger<LeagueDeactivationJob> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Starting {JobName}", nameof(LeagueDeactivationJob));

            try
            {
                var now = _dateTimeProvider.UtcNow();

                // now - EndsOn >= grace  ⟺  EndsOn <= now - grace. Expressed as a
                // cutoff so the comparison translates cleanly at the DB layer.
                var cutoff = now.AddDays(-DeactivationGraceDays);

                // Tracked (not AsNoTracking): these rows are being updated.
                var toDeactivate = await _dataContext.PickemGroups
                    .Where(g => g.DeactivatedUtc == null
                                && g.EndsOn != null
                                && g.EndsOn <= cutoff)
                    .ToListAsync();

                _logger.LogInformation(
                    "Found {Count} league(s) past the {GraceDays}-day post-EndsOn window to deactivate.",
                    toDeactivate.Count, DeactivationGraceDays);

                foreach (var group in toDeactivate)
                {
                    group.DeactivatedUtc = now;
                    _logger.LogInformation(
                        "Deactivating league {LeagueId} ({LeagueName}); EndsOn={EndsOn:o}",
                        group.Id, group.Name, group.EndsOn);
                }

                if (toDeactivate.Count > 0)
                {
                    await _dataContext.SaveChangesAsync();
                }

                _logger.LogInformation(
                    "Completed {JobName}: {Count} league(s) deactivated.",
                    nameof(LeagueDeactivationJob), toDeactivate.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {JobName}", nameof(LeagueDeactivationJob));
                throw;
            }
        }
    }
}
