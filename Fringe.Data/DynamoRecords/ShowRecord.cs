using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

[DynamoDBTable("fringe")]
public class ShowRecord
{
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    [DynamoDBGlobalSecondaryIndexHashKey("entity-type-index", AttributeName = "entityType")]
    public string EntityType { get; set; } = "SHOW";

    public int ShowId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? PlainTextDescription { get; set; }
    public string? ImageUrl { get; set; }
    public string? Tag { get; set; }
    public string Price { get; set; } = null!;
    public string Fee { get; set; } = null!;
    public string? FirstShowDate { get; set; }
    public int LengthInMinutes { get; set; }
    public VenueData? Venue { get; set; }
    public ContentRatingData? ContentRating { get; set; }
}

public class VenueData
{
    public int VenueNumber { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
}

public class ContentRatingData
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
}
