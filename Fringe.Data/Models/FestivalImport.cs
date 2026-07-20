using System.Collections.ObjectModel;

namespace Fringe.Data.Models;

/// <summary>
/// Normalized festival dataset — the source-independent persistence boundary. Any
/// origin (website scraper, API import, JSON/CSV upload, manual entry) populates
/// this shape before handing it to <see cref="FringeRepository"/>, so no import
/// source is treated as the canonical source of truth.
/// </summary>
public class FestivalImport
{
    /// <summary>Gets the shows to persist.</summary>
    public Collection<Show> Shows { get; init; } = [];

    /// <summary>Gets the showtimes to persist.</summary>
    public Collection<ShowTime> ShowTimes { get; init; } = [];

    /// <summary>Gets the venues to persist.</summary>
    public Collection<Venue> Venues { get; init; } = [];
}
