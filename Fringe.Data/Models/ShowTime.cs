namespace FringeScraper.Models;

public class ShowTime
{
    public int ShowId { get; set; }
    public DateTime DateTime { get; set; }
    public TimeOnly PerformanceTime { get; set; }
    public string PerformanceDate { get; set; } = null!;
    public string PresentationFormat { get; set; } = null!;
    public bool Reserved { get; set; }
}
