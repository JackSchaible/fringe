namespace Fringe.Data.Models;

/// <summary>Domain model representing a Fringe festival show.</summary>
public class Show
{
    /// <summary>Gets or sets the show identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the show title.</summary>
    public string Title { get; set; } = null!;

    /// <summary>Gets or sets the HTML description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the plain-text description.</summary>
    public string? PlainTextDescription { get; set; }

    /// <summary>Gets or sets the show poster image URI.</summary>
    public Uri? ImageUrl { get; set; }

    /// <summary>Gets or sets the genre tag.</summary>
    public string? Tag { get; set; }

    /// <summary>Gets or sets the ticket price excluding fees.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the per-ticket service fee.</summary>
    public decimal Fee { get; set; }

    /// <summary>Gets or sets the date of the first performance.</summary>
    public DateOnly FirstShowDate { get; set; }

    /// <summary>Gets or sets the run time in minutes.</summary>
    public int LengthInMinutes { get; set; }

    /// <summary>Gets or sets the venue.</summary>
    public Venue Venue { get; set; } = null!;

    /// <summary>Gets or sets the content rating.</summary>
    public ContentRating ContentRating { get; set; } = null!;
}
