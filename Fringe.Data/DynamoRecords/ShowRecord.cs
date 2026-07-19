using Amazon.DynamoDBv2.DataModel;
using Fringe.Data.DynamoConverters;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a Fringe show.</summary>
[DynamoDBTable("fringe")]
public class ShowRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    /// <summary>Gets or sets the entity type used for the GSI.</summary>
    [DynamoDBGlobalSecondaryIndexHashKey("entity-type-index", AttributeName = "entityType")]
    public string EntityType { get; set; } = "SHOW";

    /// <summary>Gets or sets the numeric show identifier.</summary>
    public int ShowId { get; set; }

    /// <summary>Gets or sets the show title.</summary>
    public string Title { get; set; } = null!;

    /// <summary>Gets or sets the HTML description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the plain-text description.</summary>
    public string? PlainTextDescription { get; set; }

    /// <summary>Gets or sets the show poster image URI.</summary>
    [DynamoDBProperty(typeof(UriDynamoConverter))]
    public Uri? ImageUrl { get; set; }

    /// <summary>Gets or sets the genre tag.</summary>
    public string? Tag { get; set; }

    /// <summary>Gets or sets the ticket price as a formatted string.</summary>
    public string Price { get; set; } = null!;

    /// <summary>Gets or sets the service fee as a formatted string.</summary>
    public string Fee { get; set; } = null!;

    /// <summary>Gets or sets the first show date in ISO-8601 format.</summary>
    public string? FirstShowDate { get; set; }

    /// <summary>Gets or sets the run time in minutes.</summary>
    public int LengthInMinutes { get; set; }

    /// <summary>Gets or sets the embedded venue data.</summary>
    public VenueData? Venue { get; set; }

    /// <summary>Gets or sets the embedded content rating data.</summary>
    public ContentRatingData? ContentRating { get; set; }
}

/// <summary>Embedded venue data stored within a <see cref="ShowRecord"/>.</summary>
public class VenueData
{
    /// <summary>Gets or sets the venue number.</summary>
    public int VenueNumber { get; set; }

    /// <summary>Gets or sets the venue name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Gets or sets the street address.</summary>
    public string Address { get; set; } = null!;

    /// <summary>Gets or sets the venue phone number.</summary>
    public string Phone { get; set; } = null!;

    /// <summary>Gets or sets the postal code.</summary>
    public string PostalCode { get; set; } = null!;
}

/// <summary>Embedded content rating data stored within a <see cref="ShowRecord"/>.</summary>
public class ContentRatingData
{
    /// <summary>Gets or sets the rating name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Gets or sets the short rating code.</summary>
    public string Code { get; set; } = null!;

    /// <summary>Gets or sets an optional description of the rating.</summary>
    public string? Description { get; set; }
}
