#nullable enable

using System.Security.Cryptography;
using System.Text;

namespace SportsData.Notification.Application.Reminders;

/// <summary>
/// MD5 over a canonical parameter encoding — a stable trace id, not
/// cryptographic. Two calls with the same inputs produce the same Guid,
/// giving a deterministic CorrelationId for log correlation across a
/// reminder's retries. Dedup itself is handled by each reminder table's
/// natural key, not this value.
/// </summary>
public static class ReminderCorrelation
{
    public static Guid DeterministicCorrelationId(string category, Guid userId, Guid scopeId, long qualifier)
    {
        // Qualifier is long so callers can pass a DateTime.Ticks version
        // anchor (ContestStart).
        var input = $"{category}|{userId:N}|{scopeId:N}|{qualifier}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    public static Guid DeterministicCorrelationId(string category, Guid userId, Guid scopeId, long q1, long q2)
    {
        // Two-qualifier overload for callers that need both a scope
        // discriminator (e.g. PickDeadline's seasonWeek) AND a fire-time
        // version anchor (deadline Ticks) in the same key. Keeping
        // seasonWeek in the input means two different weeks of the same
        // league with the same deadline (rare but theoretically possible
        // across year boundaries) still hash distinctly.
        var input = $"{category}|{userId:N}|{scopeId:N}|{q1}|{q2}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
