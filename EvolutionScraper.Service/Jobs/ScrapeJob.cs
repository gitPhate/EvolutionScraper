using EvolutionScraper.Service.Support.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace EvolutionScraper.Service.Jobs
{
    [DisallowConcurrentExecution]
    internal class ScrapeJob(ILogger<ScrapeJob> logger, EvolutionScraperOptions options) : BaseJob(logger)
    {
        protected override async Task ExecuteImplAsync(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, string> data = context.MergedJobDataMap
                .Where(x => x.Value is string)
                .ToDictionary(x => x.Key, x => (string)x.Value!);

            BookingJobData booking = BookingJobData.Parse(data);

            // Quartz re-fires the trigger when the job throws, according to the retry policy set in Program.cs
            int maxAttempts = (context.RetryPolicy?.MaxAttempts ?? 0) + 1;
            bool isLastAttempt = context.RetryAttempt + 1 >= maxAttempts;

            try
            {
                using EvolutionScraper scraper = new(options, logger, context.JobDetail.Key.Name);

                logger.LogInformation($"Time to book: {booking.ClassName} scheduled for {booking.DayOfWeek} at {booking.Time} (attempt {context.RetryAttempt + 1}/{maxAttempts})");
                logger.LogInformation("Starting the booking process");

                bool isBooked = await scraper.BookClassAsync(booking.ClassName, booking.DayOfWeek, booking.Time).ConfigureAwait(false);

                if (!isBooked)
                {
                    throw new InvalidOperationException("Something went wrong with booking: the booking was not confirmed");
                }

                logger.LogInformation("Booked successfully");
            }
            catch (Exception) when (isLastAttempt)
            {
                logger.LogError($"Booking of {booking.ClassName} failed on the last attempt, giving up");
                throw;
            }
        }
    }
}
