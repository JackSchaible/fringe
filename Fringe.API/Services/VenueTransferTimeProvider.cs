using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;

namespace Fringe.API.Services;

/// <summary>
/// Default <see cref="IVenueTransferTimeProvider"/>. Loads the active transfer matrix from
/// <see cref="FringeRepository"/> at most once per instance — since this is registered scoped
/// (one instance per request, matching <see cref="FringeRepository"/>'s own lifetime), a whole
/// schedule build reuses one matrix load instead of paying for it on every venue-pair check.
/// </summary>
internal sealed class VenueTransferTimeProvider(FringeRepository repository, TransferPolicyOptions options) : IVenueTransferTimeProvider
{
    /// <summary>Absolute floor for the missing-data fallback, used only if <see cref="TransferPolicyOptions.MissingDataFallback"/> is non-positive (unset/misconfigured).</summary>
    private static readonly TimeSpan minimumFallback = TimeSpan.FromMinutes(60);

    private List<TransferMatrixPairRecord>? cachedPairs;

    /// <inheritdoc/>
    public async Task<TransferGapResult> GetRequiredGapAsync(int fromVenueNumber, int toVenueNumber, TravelMode mode)
    {
        if (fromVenueNumber == toVenueNumber)
        {
            return Result(fromVenueNumber, toVenueNumber, mode, TransferRuleApplied.SameVenue, options.SameVenueGap, rawDuration: null, overhead: TimeSpan.Zero);
        }

        TransferOverride? overrideMatch = options.Overrides.FirstOrDefault(o =>
            o.FromVenueNumber == fromVenueNumber && o.ToVenueNumber == toVenueNumber && o.Mode == mode);
        if (overrideMatch != null)
        {
            TimeSpan overhead = TotalOverhead();
            return Result(fromVenueNumber, toVenueNumber, mode, TransferRuleApplied.DirectionalOverride, overrideMatch.Duration + overhead, overrideMatch.Duration, overhead);
        }

        List<TransferMatrixPairRecord> pairs = await GetPairsAsync().ConfigureAwait(false);
        TransferMatrixPairRecord? pair = pairs.Find(p => p.FromVenueNumber == fromVenueNumber && p.ToVenueNumber == toVenueNumber);
        if (pair != null)
        {
            var raw = TimeSpan.FromSeconds(DurationSecondsFor(pair, mode));
            TimeSpan overhead = TotalOverhead();
            return Result(fromVenueNumber, toVenueNumber, mode, TransferRuleApplied.Matrix, raw + overhead, raw, overhead);
        }

        TimeSpan fallback = options.MissingDataFallback > TimeSpan.Zero ? options.MissingDataFallback : minimumFallback;
        return Result(fromVenueNumber, toVenueNumber, mode, TransferRuleApplied.MissingDataFallback, fallback, rawDuration: null, overhead: TimeSpan.Zero);
    }

    private static double DurationSecondsFor(TransferMatrixPairRecord pair, TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Walking => pair.WalkingDurationSeconds,
            TravelMode.Cycling => pair.CyclingDurationSeconds,
            TravelMode.Driving => pair.DrivingDurationSeconds,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static TransferGapResult Result(int from, int to, TravelMode mode, TransferRuleApplied rule, TimeSpan requiredGap, TimeSpan? rawDuration, TimeSpan overhead)
    {
        return new TransferGapResult
        {
            FromVenueNumber = from,
            ToVenueNumber = to,
            Mode = mode,
            AppliedRule = rule,
            RawDuration = rawDuration,
            Overhead = overhead,
            RequiredGap = requiredGap
        };
    }

    private TimeSpan TotalOverhead()
    {
        return options.DepartureOverhead + options.ArrivalOverhead + options.ReliabilityBuffer;
    }

    private async Task<List<TransferMatrixPairRecord>> GetPairsAsync()
    {
        if (cachedPairs != null)
        {
            return cachedPairs;
        }

        ActiveTransferMatrixRecord? active = await repository.GetActiveTransferMatrixPointerAsync().ConfigureAwait(false);
        cachedPairs = active == null
            ? []
            : await repository.GetTransferMatrixPairsAsync(active.InputHash).ConfigureAwait(false);
        return cachedPairs;
    }
}
