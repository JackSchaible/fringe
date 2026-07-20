namespace FringeTransferMatrix.Services;

/// <summary>
/// The result of requesting a full NxN duration/distance matrix for one travel mode. A
/// non-2xx response, an unparsable response, or a response missing durations/distances is
/// modeled as an explicit failure rather than a partially-filled matrix — the generator never
/// has to guess whether missing data means "unreachable" or "the provider request broke."
/// </summary>
internal sealed class MatrixOutcome
{
    /// <summary>Gets a value indicating whether the provider returned a matrix.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>Gets the NxN duration matrix in seconds (row = origin, column = destination). Only set when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public IReadOnlyList<IReadOnlyList<double?>>? Durations { get; private init; }

    /// <summary>Gets the NxN distance matrix in meters (row = origin, column = destination). Only set when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public IReadOnlyList<IReadOnlyList<double?>>? Distances { get; private init; }

    /// <summary>Gets the human-readable failure reason. Only set when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public string? FailureReason { get; private init; }

    /// <summary>Creates a successful outcome carrying the full matrix.</summary>
    public static MatrixOutcome Succeeded(IReadOnlyList<IReadOnlyList<double?>> durations, IReadOnlyList<IReadOnlyList<double?>> distances)
    {
        return new MatrixOutcome { IsSuccess = true, Durations = durations, Distances = distances };
    }

    /// <summary>Creates a failed outcome with a reason for review.</summary>
    public static MatrixOutcome Failed(string reason)
    {
        return new MatrixOutcome { IsSuccess = false, FailureReason = reason };
    }
}
