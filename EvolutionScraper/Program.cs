
using EvolutionScraper;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

Logger logger = LogManager.GetCurrentClassLogger();
try
{
    logger.Info("Evolution scraper v1.0");

    logger.Info("Reading configs");

    Settings settings = Settings.NewFromFile();
    bool somethingFound = false;

    foreach (var kvp in settings.Bookings)
    {
        DayOfWeek dayOfWeek = kvp.Key;
        ClassBooking[] bookings = kvp.Value;

        DayOfWeek triggerDay = (DayOfWeek)((int)(dayOfWeek - 3 + 7) % 7);
        if (DateTime.Now.DayOfWeek != triggerDay)
        {
            continue;
        }

        foreach(ClassBooking booking in bookings)
        {
            somethingFound = true;
            logger.Info($"Time to book: {booking} scheduled for {dayOfWeek} at {booking.Time}");

            using ILoggerFactory loggerFactory = new NLogLoggerFactory();
            using EvolutionScraper.EvolutionScraper scraper = new(settings.EvolutionScraperOptions, loggerFactory.CreateLogger<EvolutionScraper.EvolutionScraper>());

            logger.Info("Booking #1 class");
            bool isBooked = await scraper.BookClassAsync(booking.Name, dayOfWeek, booking.Time);
            if (isBooked)
            {
                logger.Info("Booked successfully");
            }
            else
            {
                logger.Info("Something went wrong with booking");
            }
        }
    }

    if (!somethingFound)
    {
        logger.Info("Nothing to book found");
    }
}
catch (Exception ex)
{
    logger.Error("An error occurred", ex);
}
finally
{
    LogManager.Shutdown();
}

