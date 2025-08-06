using System;
using System.Collections.Generic;

namespace FringeScraper.Models;

public partial class UserRating
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ShowId { get; set; }

    public int RatingId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Rating Rating { get; set; } = null!;

    public virtual Show Show { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
