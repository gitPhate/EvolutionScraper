namespace EvolutionScraper.Service.Support.Quartz
{
    public enum JobScheduleType { Simple, Cron }

    public record QuartzJobSchedule(string FullTypeName, bool IsActive, JobScheduleType ScheduleType, string? CronValue, int? SimpleValueInSeconds, int? StartAfterInSeconds, bool? StartImmediatly)
    {
        public QuartzJobSchedule() : this(string.Empty, false, JobScheduleType.Simple, null, null, null, null)
        {
        }
    }
    public record QuartzConfig(int JobRescheduleIntervalInSeconds, QuartzJobSchedule[] Jobs)
    {
        public QuartzConfig() : this(0, [])
        {
        }
    }
}
