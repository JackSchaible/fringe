namespace Fringe.Data.Models;

/// <summary>Domain model representing a Fringe content rating.</summary>
public class ContentRating
{
    /// <summary>Gets or sets the rating name (e.g. "General").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Gets or sets the short rating code (e.g. "G").</summary>
    public string Code { get; set; } = null!;

    /// <summary>Gets or sets an optional description of the rating.</summary>
    public string? Description { get; set; }
}
