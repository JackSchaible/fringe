namespace FringeScraper.Models;

public class Venue
{
    public int VenueNumber { get; set; }
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
}
