using EvolutionScraper.Service.Support.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace EvolutionScraper.Service.Jobs
{
    [DisallowConcurrentExecution]
    internal class ScrapeJob(ILogger<ScrapeJob> logger, EvolutionScraperOptions options, Dictionary<DayOfWeek, ClassBooking[]> bookings) : BaseJob(logger)
    {
        protected override async Task ExecuteImplAsync(IJobExecutionContext context)
        {
            bool somethingFound = false;
            using EvolutionScraper scraper = new(options, logger);

            foreach (var kvp in bookings)
            {
                DayOfWeek dayOfWeek = kvp.Key;
                ClassBooking[] bookings = kvp.Value;

                if (!BookingHelper.IsBookingDay(dayOfWeek, DateTime.Today))
                {
                    continue;
                }

                foreach (ClassBooking booking in bookings)
                {
                    try
                    {
                        somethingFound = true;
                        logger.LogInformation($"Time to book: {booking.Name} scheduled for {dayOfWeek} at {booking.Time}");

                        logger.LogInformation("Starting the booking process");
                        bool isBooked = await scraper.BookClassAsync(booking.Name, dayOfWeek, booking.Time).ConfigureAwait(false);
                        if (isBooked)
                        {
                            logger.LogInformation("Booked successfully");
                        }
                        else
                        {
                            logger.LogInformation("Something went wrong with booking");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"An error occurred in job {context.JobDetail.Key.Name.Trim()}");
                    }
                }
            }

            if (!somethingFound)
            {
                logger.LogInformation("Nothing to book found");
            }
        }
    }
}
