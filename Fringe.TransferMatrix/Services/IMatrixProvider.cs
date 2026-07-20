using Fringe.Data.Models;

namespace FringeTransferMatrix.Services;

/// <summary>Resolves a full NxN duration/distance matrix across a set of venues for one travel mode.</summary>
internal interface IMatrixProvider
{
    /// <summary>
    /// Requests the durations and distances between every pair of <paramref name="venues"/> for
    /// <paramref name="mode"/>, in the order given — row/column <c>i</c> of the returned matrix
    /// corresponds to <c>venues[i]</c>.
    /// </summary>
    Task<MatrixOutcome> GetMatrixAsync(TravelMode mode, IReadOnlyList<(int VenueNumber, double Latitude, double Longitude)> venues, CancellationToken cancellationToken = default);
}
