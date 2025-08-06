namespace FringeScraper.Services;

using Microsoft.EntityFrameworkCore;
using Models;

public class DatabaseInserter(string connectionString)
{
    public async Task InsertDataAsync(
        List<Show> shows, 
        List<Venue> venues, 
        List<ContentRating> ratings, 
        List<ShowTime> showTimes)
    {
        Console.WriteLine("🔄 Inserting data into the database...");
        FringeDbContext db = new(connectionString);

        Console.WriteLine("🗑️ Clearing existing data...");
        await db.ShowTimes.ExecuteDeleteAsync();
        await db.UserRatings.ExecuteDeleteAsync();
        await db.Shows.ExecuteDeleteAsync();
        await db.Venues.ExecuteDeleteAsync();
        await db.ContentRatings.ExecuteDeleteAsync();

        await db.SaveChangesAsync();
        
        Console.WriteLine("📥 Inserting new venues and content ratings...");
        await db.ContentRatings.AddRangeAsync(ratings);
        await db.Venues.AddRangeAsync(venues);
        await db.SaveChangesAsync();
        
        Console.WriteLine("📥 Inserting new shows...");
        foreach(Show show in shows)
        {
            show.VenueId = venues.First(v => v.VenueNumber == show.Venue.VenueNumber).Id;
            show.ContentRatingId = ratings.First(r => r.Code == show.ContentRating.Code).Id;
        }
        await db.Shows.AddRangeAsync(shows);
        await db.SaveChangesAsync();
        
        Console.WriteLine("📥 Inserting new showtimes...");
        
        await db.ShowTimes.AddRangeAsync(showTimes);
        await db.SaveChangesAsync();
    }
}