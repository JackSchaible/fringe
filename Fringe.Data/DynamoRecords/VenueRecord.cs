using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a canonical Fringe venue.</summary>
[DynamoDBTable("fringe")]
public class VenueRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    /// <summary>Gets or sets the entity type used for the GSI.</summary>
    [DynamoDBGlobalSecondaryIndexHashKey("entity-type-index", AttributeName = "entityType")]
    public string EntityType { get; set; } = "VENUE";

    /// <summary>Gets or sets the numeric venue identifier.</summary>
    public int VenueNumber { get; set; }

    /// <summary>Gets or sets the venue name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Gets or sets the street address.</summary>
    public string Address { get; set; } = null!;

    /// <summary>Gets or sets the venue phone number.</summary>
    public string Phone { get; set; } = null!;

    /// <summary>Gets or sets the postal code.</summary>
    public string PostalCode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the latitude. Populated by a separate geocoding/enrichment
    /// process, not by festival-data imports — <c>SaveVenuesAsync</c> preserves
    /// this value across re-imports rather than overwriting it.
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude. Populated by a separate geocoding/enrichment
    /// process, not by festival-data imports — <c>SaveVenuesAsync</c> preserves
    /// this value across re-imports rather than overwriting it.
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Gets or sets the hash of the routing-relevant address fields (venue number,
    /// normalized street address, normalized postal code) that produced the current
    /// <see cref="Latitude"/>/<see cref="Longitude"/>. Used to detect when the venue's
    /// location actually changed and re-geocoding is needed, without re-geocoding on
    /// every unrelated festival-data import.
    /// </summary>
    public string? AddressHash { get; set; }

    /// <summary>
    /// Gets or sets where the current coordinates came from — a geocoding provider
    /// name (e.g. "OpenRouteService"), or "Manual" for a human-confirmed override.
    /// A "Manual" value must never be overwritten by automatic geocoding.
    /// </summary>
    public string? CoordinateSource { get; set; }

    /// <summary>Gets or sets the ISO-8601 timestamp the coordinates were last set.</summary>
    public string? EnrichedAt { get; set; }
}
