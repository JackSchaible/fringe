using System.Collections.ObjectModel;

namespace Fringe.Data.Models;

/// <summary>
/// Domain model for one complete, validated transfer-matrix version, ready to persist.
/// <see cref="FringeRepository.SaveTransferMatrixAsync"/> never receives a partial version —
/// validation happens before this is constructed, so nothing invalid ever reaches storage.
/// </summary>
public class TransferMatrixVersion
{
    /// <summary>Gets or sets the routing input hash that identifies this version.</summary>
    public string InputHash { get; set; } = null!;

    /// <summary>Gets or sets the number of venues that participated in this version.</summary>
    public int VenueCount { get; set; }

    /// <summary>Gets or sets the UTC generation timestamp.</summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>Gets or sets the routing provider that generated this version.</summary>
    public string Source { get; set; } = null!;

    /// <summary>Gets the directional pairs in this version.</summary>
    public Collection<TransferPair> Pairs { get; init; } = [];
}
