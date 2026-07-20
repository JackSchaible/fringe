using Fringe.Data.Models;

namespace Fringe.API.Services;

/// <summary>
/// Resolves the scheduling gap a group needs between two venues, without scheduling code ever
/// needing to know about routing providers, matrix storage, or DynamoDB — it depends on this
/// abstraction only. Backed by the active persisted transfer matrix (see <c>Fringe.TransferMatrix</c>);
/// never calls an external routing service itself.
/// </summary>
internal interface IVenueTransferTimeProvider
{
    /// <summary>
    /// Returns the required gap between leaving <paramref name="fromVenueNumber"/> and arriving at
    /// <paramref name="toVenueNumber"/> for <paramref name="mode"/>, resolved via same-venue policy,
    /// then a directional override, then the active matrix, then a conservative fallback — in that order.
    /// </summary>
    Task<TransferGapResult> GetRequiredGapAsync(int fromVenueNumber, int toVenueNumber, TravelMode mode);
}
