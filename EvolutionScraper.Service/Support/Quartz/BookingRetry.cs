namespace EvolutionScraper.Service.Support.Quartz
{
    internal static class BookingRetry
    {
        public const string AttemptKey = "Attempt";

        /// <param name="retriesDone">Retries already scheduled before the current execution (0 for the regular run).</param>
        public static bool CanRetry(int retriesDone, int maxRetries) => retriesDone < maxRetries;

        public static string GetRetryTriggerName(string jobName, int retryNumber) => $"{jobName}-retry-{retryNumber}";
    }
}
