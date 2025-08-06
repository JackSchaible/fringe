using System;
using System.Collections.Generic;

namespace FringeScraper.Models;

public partial class Show
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string? PlainTextDescription { get; set; }

    public string? ImageUrl { get; set; }

    public string? Tag { get; set; }

    public decimal Price { get; set; }

    public decimal Fee { get; set; }

    public DateOnly FirstShowDate { get; set; }

    public int LengthInMinutes { get; set; }

    public int VenueId { get; set; }

    public int ContentRatingId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ContentRating ContentRating { get; set; } = null!;

    public virtual ICollection<ShowTime> ShowTimes { get; set; } = new List<ShowTime>();

    public virtual ICollection<UserRating> UserRatings { get; set; } = new List<UserRating>();

    public virtual Venue Venue { get; set; } = null!;
}
