using System;
using System.Collections.Generic;

namespace FringeScraper.Models;

public partial class ShowTime
{
    public int Id { get; set; }

    public int ShowId { get; set; }

    public DateTime DateTime { get; set; }

    public TimeOnly PerformanceTime { get; set; }

    public string PerformanceDate { get; set; } = null!;

    public string PresentationFormat { get; set; } = null!;

    public bool Reserved { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Show Show { get; set; } = null!;
}
