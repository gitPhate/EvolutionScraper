
using EvolutionScraper;

Console.WriteLine("Evolution scraper v1.0");

Console.WriteLine("Reading configs");

Settings settings = Settings.NewFromFile();
bool somethingFound = false;

foreach (var kvp in settings.Bookings)
{
    DayOfWeek triggerDay = (DayOfWeek)((int)(kvp.Key - 3 + 7) % 7);
    if (DateTime.Now.DayOfWeek != triggerDay)
    {
        continue;
    }

    somethingFound = true;
    Console.WriteLine($"Time to book: {kvp.Value.Name} scheduled for {kvp.Key} at {kvp.Value.Time}");
    using EvolutionScraper.EvolutionScraper scraper = new(settings.EvolutionScraperOptions);

    Console.WriteLine("Booking #1 class");
    bool isBooked = await scraper.BookClassAsync(kvp.Value.Name, kvp.Key, kvp.Value.Time);
    if (isBooked)
    {
        Console.WriteLine("Booked successfully");
    }
    else
    {
        Console.WriteLine("Something went wrong with booking");
    }
}

if (!somethingFound)
{
    Console.WriteLine("Nothing to book found");
}

