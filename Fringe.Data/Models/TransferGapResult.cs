namespace Fringe.Data.Models;

/// <summary>Which rule resolved a venue-transfer gap requirement, in precedence order.</summary>
public enum TransferRuleApplied
{
    /// <summary>Origin and destination are the same physical venue.</summary>
    SameVenue,

    /// <summary>An explicit directional override for this venue pair and mode was configured.</summary>
    DirectionalOverride,

    /// <summary>The active transfer matrix had a value for this venue pair and mode.</summary>
    Matrix,

    /// <summary>No override or matrix data was available; a conservative fallback was used.</summary>
    MissingDataFallback
}

/// <summary>
/// The resolved time gap a group needs between leaving <see cref="FromVenueNumber"/> and
/// arriving at <see cref="ToVenueNumber"/>, plus enough detail to explain how it was derived —
/// which rule fired, what raw travel duration (if any) fed into it, and how much scheduling
/// overhead was layered on top. <see cref="RawDuration"/> and <see cref="Overhead"/> are kept
/// separate from <see cref="RequiredGap"/> so either can change without the other.
/// </summary>
public class TransferGapResult
{
    /// <summary>Gets or sets the origin venue number.</summary>
    public int FromVenueNumber { get; set; }

    /// <summary>Gets or sets the destination venue number.</summary>
    public int ToVenueNumber { get; set; }

    /// <summary>Gets or sets the travel mode this result was resolved for.</summary>
    public TravelMode Mode { get; set; }

    /// <summary>Gets or sets which precedence rule produced <see cref="RequiredGap"/>.</summary>
    public TransferRuleApplied AppliedRule { get; set; }

    /// <summary>
    /// Gets or sets the raw travel duration (override or matrix value) before scheduling overhead
    /// was added. <see langword="null"/> for <see cref="TransferRuleApplied.SameVenue"/> and
    /// <see cref="TransferRuleApplied.MissingDataFallback"/>, neither of which have one.
    /// </summary>
    public TimeSpan? RawDuration { get; set; }

    /// <summary>Gets or sets the total scheduling overhead added on top of <see cref="RawDuration"/>.</summary>
    public TimeSpan Overhead { get; set; }

    /// <summary>Gets or sets the total required gap — what scheduling code must actually enforce.</summary>
    public TimeSpan RequiredGap { get; set; }
}
