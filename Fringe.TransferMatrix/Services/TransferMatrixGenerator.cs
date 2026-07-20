using System.Globalization;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;

namespace FringeTransferMatrix.Services;

/// <summary>
/// Generates and atomically publishes a new transfer-matrix version when the routable venue set
/// has changed or a refresh is due, and exits without calling the routing provider otherwise.
/// A version is only ever persisted (and only ever promoted) once every expected pair and mode
/// has been validated — nothing partial or invalid can become the active matrix.
/// <paramref name="requestDelay"/> is the pause between the three per-mode provider calls
/// (defaults to a rate-limit-friendly 1s); tests pass <see cref="TimeSpan.Zero"/>.
/// <paramref name="refreshInterval"/> and <paramref name="staleRetention"/> default to 30 days each.
/// </summary>
internal sealed class TransferMatrixGenerator(
    FringeRepository repository,
    IMatrixProvider provider,
    TimeSpan? requestDelay = null,
    TimeSpan? refreshInterval = null,
    TimeSpan? staleRetention = null)
{
    private const string coordinateSource = "OpenRouteService";
    private static readonly TimeSpan defaultRequestDelay = TimeSpan.FromMilliseconds(1000);

    /// <summary>How long an unchanged active matrix is trusted before an age-based refresh is due.</summary>
    private static readonly TimeSpan defaultRefreshInterval = TimeSpan.FromDays(30);

    /// <summary>How long a superseded matrix version is kept around (via DynamoDB TTL) before it's eligible for cleanup.</summary>
    private static readonly TimeSpan defaultStaleRetention = TimeSpan.FromDays(30);

    private readonly TimeSpan delay = requestDelay ?? defaultRequestDelay;
    private readonly TimeSpan refresh = refreshInterval ?? defaultRefreshInterval;
    private readonly TimeSpan staleAfter = staleRetention ?? defaultStaleRetention;

    /// <summary>Runs one generation attempt.</summary>
    public async Task GenerateAsync()
    {
        List<VenueRecord> venues = await repository.GetAllVenuesAsync().ConfigureAwait(false);
        List<(int VenueNumber, double Latitude, double Longitude)> routable = [.. venues
            .Where(v => v.Latitude != null && v.Longitude != null)
            .Select(v => (v.VenueNumber, Latitude: v.Latitude!.Value, Longitude: v.Longitude!.Value))
            .OrderBy(v => v.VenueNumber)];

        if (routable.Count < 2)
        {
            TransferMatrixLogger.Log("Fewer than 2 routable (geocoded) venues — nothing to generate.");
            return;
        }

        string inputHash = TransferMatrixHasher.ComputeHash(routable);
        ActiveTransferMatrixRecord? active = await repository.GetActiveTransferMatrixPointerAsync().ConfigureAwait(false);

        if (active != null && string.Equals(active.InputHash, inputHash, StringComparison.Ordinal) && !IsRefreshDue(active))
        {
            TransferMatrixLogger.Log("Transfer matrix inputs unchanged and no refresh due — skipping.");
            return;
        }

        TransferMatrixLogger.Log(string.Create(CultureInfo.InvariantCulture,
            $"Generating transfer matrix for {routable.Count} venues (hash {inputHash})..."));

        Dictionary<TravelMode, MatrixOutcome> outcomes = [];
        foreach (TravelMode mode in Enum.GetValues<TravelMode>())
        {
            MatrixOutcome outcome = await provider.GetMatrixAsync(mode, routable).ConfigureAwait(false);
            if (!outcome.IsSuccess)
            {
                TransferMatrixLogger.LogError(string.Create(CultureInfo.InvariantCulture,
                    $"Matrix request failed for {mode}: {outcome.FailureReason}. Aborting — previous active matrix remains in effect."));
                return;
            }
            outcomes[mode] = outcome;
            await Task.Delay(delay).ConfigureAwait(false);
        }

        List<TransferPair>? pairs = BuildPairs(routable, outcomes);
        if (pairs == null)
        {
            TransferMatrixLogger.LogError("Matrix validation failed — previous active matrix remains in effect.");
            return;
        }

        DateTime generatedAt = DateTime.UtcNow;
        TransferMatrixVersion version = new()
        {
            InputHash = inputHash,
            VenueCount = routable.Count,
            GeneratedAt = generatedAt,
            Source = coordinateSource
        };
        foreach (TransferPair pair in pairs)
        {
            version.Pairs.Add(pair);
        }

        await repository.SaveTransferMatrixAsync(version).ConfigureAwait(false);
        await repository.SetActiveTransferMatrixAsync(inputHash, generatedAt).ConfigureAwait(false);
        TransferMatrixLogger.Log(string.Create(CultureInfo.InvariantCulture,
            $"Published transfer matrix {inputHash} with {pairs.Count} pairs."));

        if (active != null && !string.Equals(active.InputHash, inputHash, StringComparison.Ordinal))
        {
            await repository.MarkTransferMatrixStaleAsync(active.InputHash, generatedAt.Add(staleAfter)).ConfigureAwait(false);
            TransferMatrixLogger.Log(string.Create(CultureInfo.InvariantCulture,
                $"Marked superseded matrix {active.InputHash} stale (retained {staleAfter.TotalDays:F0} more days)."));
        }
    }

    private bool IsRefreshDue(ActiveTransferMatrixRecord active)
    {
        return !DateTime.TryParse(active.PromotedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime promotedAt)
            || DateTime.UtcNow - promotedAt >= refresh;
    }

    private static List<TransferPair>? BuildPairs(
        List<(int VenueNumber, double Latitude, double Longitude)> venues,
        Dictionary<TravelMode, MatrixOutcome> outcomes)
    {
        int n = venues.Count;
        foreach (TravelMode mode in Enum.GetValues<TravelMode>())
        {
            if (!HasExpectedDimensions(outcomes[mode].Durations, n) || !HasExpectedDimensions(outcomes[mode].Distances, n))
            {
                TransferMatrixLogger.LogError(string.Create(CultureInfo.InvariantCulture,
                    $"Matrix for {mode} has unexpected dimensions (expected {n}x{n})."));
                return null;
            }
        }

        List<TransferPair> pairs = new(n * (n - 1));
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (i == j)
                {
                    continue;
                }

                TransferPair? pair = TryBuildPair(venues[i].VenueNumber, venues[j].VenueNumber, i, j, outcomes);
                if (pair == null)
                {
                    return null;
                }
                pairs.Add(pair);
            }
        }

        return pairs;
    }

    private static TransferPair? TryBuildPair(int fromVenueNumber, int toVenueNumber, int i, int j, Dictionary<TravelMode, MatrixOutcome> outcomes)
    {
        double? walkDuration = outcomes[TravelMode.Walking].Durations![i][j];
        double? walkDistance = outcomes[TravelMode.Walking].Distances![i][j];
        double? cycleDuration = outcomes[TravelMode.Cycling].Durations![i][j];
        double? cycleDistance = outcomes[TravelMode.Cycling].Distances![i][j];
        double? driveDuration = outcomes[TravelMode.Driving].Durations![i][j];
        double? driveDistance = outcomes[TravelMode.Driving].Distances![i][j];

        if (walkDuration == null || walkDistance == null
            || cycleDuration == null || cycleDistance == null
            || driveDuration == null || driveDistance == null)
        {
            TransferMatrixLogger.LogError(string.Create(CultureInfo.InvariantCulture,
                $"Missing route data for venue {fromVenueNumber} -> {toVenueNumber}."));
            return null;
        }

        return new TransferPair
        {
            FromVenueNumber = fromVenueNumber,
            ToVenueNumber = toVenueNumber,
            WalkingDurationSeconds = walkDuration.Value,
            WalkingDistanceMeters = walkDistance.Value,
            CyclingDurationSeconds = cycleDuration.Value,
            CyclingDistanceMeters = cycleDistance.Value,
            DrivingDurationSeconds = driveDuration.Value,
            DrivingDistanceMeters = driveDistance.Value,
            Source = coordinateSource
        };
    }

    private static bool HasExpectedDimensions(IReadOnlyList<IReadOnlyList<double?>>? matrix, int n)
    {
        return matrix != null && matrix.Count == n && matrix.All(row => row.Count == n);
    }
}
