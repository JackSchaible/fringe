using System;
using System.Collections.Generic;

namespace FringeScraper.Models;

public partial class Venue
{
    public int Id { get; set; }

    public int VenueNumber { get; set; }

    public string Name { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string PostalCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Show> Shows { get; set; } = new List<Show>();
}
