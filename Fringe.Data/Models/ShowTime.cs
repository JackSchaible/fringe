namespace Fringe.Data.Models;

/// <summary>Domain model representing a single performance of a show.</summary>
public class ShowTime
{
    /// <summary>Gets or sets the show identifier.</summary>
    public int ShowId { get; set; }

    /// <summary>Gets or sets the UTC start date and time of the performance.</summary>
    public DateTime DateTime { get; set; }

    /// <summary>Gets or sets the local performance time.</summary>
    public TimeOnly PerformanceTime { get; set; }

    /// <summary>Gets or sets the human-readable performance date string.</summary>
    public string PerformanceDate { get; set; } = null!;

    /// <summary>Gets or sets the presentation format (e.g. "In-Person").</summary>
    public string PresentationFormat { get; set; } = null!;

    /// <summary>Gets or sets a value indicating whether seating is reserved.</summary>
    public bool Reserved { get; set; }
}
