using Microsoft.Extensions.Logging;
using Quartz;

namespace EvolutionScraper.Service.Support.Quartz
{
    internal abstract class BaseJob(ILogger logger) : IJob
    {
        protected abstract Task ExecuteImplAsync(IJobExecutionContext context, CancellationToken cancellationToken = default);

        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            string jobName = context.JobDetail.Key.Name.Trim();

            try
            {
                logger.LogInformation($"-- Executing job {jobName} --");
                await ExecuteImplAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred in job {jobName}");

                // Rethrown so Quartz sees the failure and applies the retry policy
                throw;
            }
            finally
            {
                logger.LogInformation($"-- Ending execution of job {jobName} --");
            }
        }
    }
}
