using System;
using System.Collections.Generic;

namespace FringeScraper.Models;

public partial class Rating
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<UserRating> UserRatings { get; set; } = new List<UserRating>();
}
