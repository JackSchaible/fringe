using Fringe.Data.Models;

namespace Fringe.API.Services;

/// <summary>
/// The reason a candidate performance was rejected by <see cref="IScheduleBuilder"/>'s
/// venue-transfer feasibility check — the exact venue pair, timing, and policy rule that made the
/// transfer infeasible, for missed-show diagnostics (FA-35). Never constructed for any other
/// rejection reason (raw time overlap, availability) — those are diagnosed separately.
/// </summary>
internal readonly record struct TransferConflictDetail(
    int OriginVenueNumber,
    int DestinationVenueNumber,
    string OriginShowTitle,
    string DestinationShowTitle,
    TimeSpan AvailableGap,
    TimeSpan RequiredGap,
    TravelMode Mode,
    TransferRuleApplied AppliedRule);
