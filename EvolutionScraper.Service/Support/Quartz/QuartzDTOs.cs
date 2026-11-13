namespace EvolutionScraper.Service.Support.Quartz
{
    public record QuartzJobSchedule(
        string Name,
        string FullTypeName,
        bool IsActive,
        int? StartAfterInSeconds,
        bool? StartImmediately,
        string? Calendar,
        Dictionary<string, string> Data)
    {
        public QuartzJobSchedule() : this(string.Empty, string.Empty, false, null, null, null, [])
        {
        }
    }

    public record QuartzConfig(int JobRescheduleIntervalInSeconds, int RetryDelayInSeconds, int MaxRetries, QuartzJobSchedule[] Jobs)
    {
        public QuartzConfig() : this(0, 60, 3, [])
        {
        }
    }
}
