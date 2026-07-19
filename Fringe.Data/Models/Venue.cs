namespace Fringe.Data.Models;

/// <summary>Domain model representing a Fringe venue.</summary>
public class Venue
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
