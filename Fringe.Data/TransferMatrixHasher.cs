using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Fringe.Data;

/// <summary>
/// Computes a stable hash over the routing-relevant venue set — venue number, latitude, and
/// longitude only — used to detect when the transfer matrix needs regenerating. Venue name,
/// phone number, address text, and anything show-related deliberately do not participate:
/// only a coordinate change (or a venue being added/removed) changes this hash.
/// </summary>
public static class TransferMatrixHasher
{
    /// <summary>
    /// Coordinate precision (decimal places) included in the hash. Bounds the hash to ~11cm
    /// resolution so floating-point representation noise from repeated geocoding of an
    /// unchanged address can't spuriously trigger a full matrix regeneration.
    /// </summary>
    private const int coordinatePrecision = 6;

    /// <summary>Computes a stable hash of the routing-relevant venue set, order-independent.</summary>
    public static string ComputeHash(IEnumerable<(int VenueNumber, double Latitude, double Longitude)> venues)
    {
        ArgumentNullException.ThrowIfNull(venues);

        IEnumerable<string> entries = venues
            .OrderBy(v => v.VenueNumber)
            .Select(v => string.Create(CultureInfo.InvariantCulture,
                $"{v.VenueNumber}:{Math.Round(v.Latitude, coordinatePrecision)}:{Math.Round(v.Longitude, coordinatePrecision)}"));

        string normalized = string.Join("|", entries);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }
}
