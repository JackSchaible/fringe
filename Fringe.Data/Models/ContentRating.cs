using System;
using System.Collections.Generic;

namespace FringeScraper.Models;

public partial class ContentRating
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Show> Shows { get; set; } = new List<Show>();
}
