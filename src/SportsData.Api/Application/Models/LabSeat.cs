using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Models;

/// <summary>
/// A family's lab seat: at most one active <see cref="Model.LabSeatGateway"/>
/// row per family (IX_Model_SingleActiveLabSeat). Activating a version
/// swaps the seat (same precedent as IsDefault: the newcomer wins and the
/// previous holder is cleared in the same SaveChanges; the index turns a
/// concurrent race into a constraint violation).
/// </summary>
internal static class LabSeat
{
    public static bool Occupies(ModelGateway gateway, bool isActive) =>
        isActive && gateway == Model.LabSeatGateway;

    /// <summary>
    /// Deactivates (tracked, unsaved) every other seat holder in the family.
    /// Returns the displaced models' names for logging.
    /// </summary>
    public static async Task<List<string>> VacateAsync(
        AppDataContext dataContext,
        Guid modelFamilyId,
        Guid incomingModelId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var holders = await dataContext.Models
            .Where(m => m.ModelFamilyId == modelFamilyId
                     && m.Id != incomingModelId
                     && m.IsActive
                     && m.Gateway == Model.LabSeatGateway)
            .ToListAsync(cancellationToken);

        foreach (var holder in holders)
        {
            holder.IsActive = false;
            holder.ModifiedUtc = nowUtc;
        }

        return holders.Select(h => h.Name).ToList();
    }
}
