namespace FringeScraper.Models;

public class Show
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
    public Venue Venue { get; set; } = null!;
    public ContentRating ContentRating { get; set; } = null!;
}
