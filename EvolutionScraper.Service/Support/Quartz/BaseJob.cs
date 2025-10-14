using Microsoft.Extensions.Logging;
using Quartz;

namespace EvolutionScraper.Service.Support.Quartz
{
    internal abstract class BaseJob(ILogger logger) : IJob
    {
        protected abstract Task ExecuteImplAsync(IJobExecutionContext context);

        public async Task Execute(IJobExecutionContext context)
        {
            string jobName = context.JobDetail.Key.Name.Trim();

            try
            {
                logger.LogInformation($"-- Executing job {jobName} --");
                await ExecuteImplAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred in job {jobName}");
            }
            finally
            {
                await TryUnscheduleImmediateStartTriggerAsync(context).ConfigureAwait(false);
                logger.LogInformation($"-- Ending execution of job {jobName} --");
            }
        }

        private async Task<bool> TryUnscheduleImmediateStartTriggerAsync(IJobExecutionContext context)
        {
            string triggerName = string.Format(QuartzConsts.StartImmediatlyTriggerName, GetType().FullName);
            TriggerKey triggerKey = new(triggerName);

            try
            {
                bool triggerExists = await context.Scheduler.CheckExists(triggerKey);
                if (!triggerExists)
                {
                    return true;
                }

                bool result =
                    await context
                        .Scheduler
                        .UnscheduleJob(new TriggerKey(triggerName)).ConfigureAwait(false);

                return result;
            }
            catch
            {
                return false;
            }
        }
    }
}
