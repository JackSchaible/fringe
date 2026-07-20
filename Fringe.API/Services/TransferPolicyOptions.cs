using System.Collections.ObjectModel;
using Fringe.Data.Models;

namespace Fringe.API.Services;

/// <summary>
/// Scheduling-side policy knobs layered on top of raw transfer-matrix data by
/// <see cref="VenueTransferTimeProvider"/>. Kept deliberately separate from the persisted
/// matrix (see <see cref="TransferGapResult.RawDuration"/> vs <see cref="TransferGapResult.Overhead"/>)
/// so either can change without regenerating or touching the other.
/// </summary>
internal class TransferPolicyOptions
{
    /// <summary>
    /// Gets or sets the required gap when origin and destination are the same physical venue.
    /// Defaults to zero — back-to-back shows in the same room need no travel time — but some
    /// festivals may want a small turnover buffer even then.
    /// </summary>
    public TimeSpan SameVenueGap { get; set; } = TimeSpan.Zero;

    /// <summary>Gets or sets the overhead added for leaving the origin venue (queuing, coat check, etc.).</summary>
    public TimeSpan DepartureOverhead { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets the overhead added for arriving at and settling into the destination venue.</summary>
    public TimeSpan ArrivalOverhead { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets a general buffer added to absorb routing/estimate inaccuracy.</summary>
    public TimeSpan ReliabilityBuffer { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets or sets the conservative gap used when neither an override nor matrix data exists for
    /// a venue pair and mode. Must never be zero — a missing route must never look like "no travel
    /// needed." <see cref="VenueTransferTimeProvider"/> enforces this regardless of what's configured here.
    /// </summary>
    public TimeSpan MissingDataFallback { get; set; } = TimeSpan.FromMinutes(60);

    /// <summary>Gets the explicit directional venue-pair overrides, checked before matrix data.</summary>
    public Collection<TransferOverride> Overrides { get; init; } = [];
}

/// <summary>
/// An explicit, directional, mode-specific override for one venue pair's transfer duration —
/// e.g. a known detour or closure the routing provider doesn't reflect. <c>(A, B)</c> and
/// <c>(B, A)</c> are independent entries; configuring one does not imply the other.
/// </summary>
internal class TransferOverride
{
    /// <summary>Gets or sets the origin venue number.</summary>
    public int FromVenueNumber { get; set; }

    /// <summary>Gets or sets the destination venue number.</summary>
    public int ToVenueNumber { get; set; }

    /// <summary>Gets or sets the travel mode this override applies to.</summary>
    public TravelMode Mode { get; set; }

    /// <summary>Gets or sets the raw transfer duration, before scheduling overhead is added.</summary>
    public TimeSpan Duration { get; set; }
}
