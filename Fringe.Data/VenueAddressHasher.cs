using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Fringe.Data;

/// <summary>
/// Computes a stable hash over the routing-relevant portion of a venue's address —
/// venue number, street address, and postal code — used to detect when a venue's
/// location actually changed and needs re-geocoding. Venue name, phone number, and
/// anything show-related deliberately do not participate in this hash.
/// </summary>
public static partial class VenueAddressHasher
{
    /// <summary>Computes a stable hash of the routing-relevant address fields.</summary>
    public static string ComputeHash(int venueNumber, string address, string postalCode)
    {
        string normalized = string.Create(CultureInfo.InvariantCulture,
            $"{venueNumber}|{NormalizeAddress(address)}|{NormalizePostalCode(postalCode)}");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Normalizes a street address for comparison: trims, collapses whitespace, and uppercases.</summary>
    public static string NormalizeAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return WhitespaceRegex().Replace(address.Trim(), " ").ToUpperInvariant();
    }

    /// <summary>Normalizes a postal code for comparison: strips whitespace and uppercases.</summary>
    public static string NormalizePostalCode(string postalCode)
    {
        ArgumentNullException.ThrowIfNull(postalCode);
        return postalCode.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
