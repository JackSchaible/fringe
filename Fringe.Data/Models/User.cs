using System;
using System.Collections.Generic;

namespace FringeScraper.Models;

public partial class User
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<UserRating> UserRatings { get; set; } = new List<UserRating>();
}
