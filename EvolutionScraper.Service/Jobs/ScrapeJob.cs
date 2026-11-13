using EvolutionScraper.Service.Support.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace EvolutionScraper.Service.Jobs
{
    [DisallowConcurrentExecution]
    internal class ScrapeJob(ILogger<ScrapeJob> logger, EvolutionScraperOptions options, QuartzConfig quartzConfig) : BaseJob(logger)
    {
        protected override async Task ExecuteImplAsync(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            JobKey jobKey = context.JobDetail.Key;
            int retriesDone = context.MergedJobDataMap.ContainsKey(BookingRetry.AttemptKey)
                ? context.MergedJobDataMap.GetInt(BookingRetry.AttemptKey)
                : 0;

            Dictionary<string, string> data = context.MergedJobDataMap
                .Where(x => x.Value is string)
                .ToDictionary(x => x.Key, x => (string)x.Value!);

            BookingJobData booking = BookingJobData.Parse(data);

            bool isBooked = false;

            try
            {
                using EvolutionScraper scraper = new(options, logger, jobKey.Name);

                logger.LogInformation($"Time to book: {booking.ClassName} scheduled for {booking.DayOfWeek} at {booking.Time} (attempt {retriesDone + 1})");
                logger.LogInformation("Starting the booking process");
                isBooked = await scraper.BookClassAsync(booking.ClassName, booking.DayOfWeek, booking.Time).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, $"An error occurred in job {jobKey.Name.Trim()}");
            }

            if (isBooked)
            {
                logger.LogInformation("Booked successfully");
                return;
            }

            logger.LogWarning("Something went wrong with booking");
            await ScheduleRetryAsync(context, retriesDone, cancellationToken).ConfigureAwait(false);
        }

        private async Task ScheduleRetryAsync(IJobExecutionContext context, int retriesDone, CancellationToken cancellationToken)
        {
            JobKey jobKey = context.JobDetail.Key;

            if (!BookingRetry.CanRetry(retriesDone, quartzConfig.MaxRetries))
            {
                logger.LogError($"Booking failed after {retriesDone + 1} attempts, giving up");
                return;
            }

            int nextRetry = retriesDone + 1;

            // The retry trigger is not bound to the booking-day calendar: it must fire regardless of the day rules
            ITrigger trigger = TriggerBuilder
                .Create()
                .WithIdentity(BookingRetry.GetRetryTriggerName(jobKey.Name, nextRetry), jobKey.Group)
                .ForJob(jobKey)
                .UsingJobData(BookingRetry.AttemptKey, nextRetry)
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(quartzConfig.RetryDelayInSeconds))
                .Build();

            await context.Scheduler.ScheduleJob(trigger, default, cancellationToken).ConfigureAwait(false);
            logger.LogInformation($"Retry {nextRetry}/{quartzConfig.MaxRetries} scheduled in {quartzConfig.RetryDelayInSeconds} seconds");
        }
    }
}
