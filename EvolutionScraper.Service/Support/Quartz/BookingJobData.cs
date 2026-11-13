using System.Globalization;

namespace EvolutionScraper.Service.Support.Quartz
{
    internal sealed record BookingJobData(string ClassName, DayOfWeek DayOfWeek, TimeOnly Time)
    {
        public const string ClassNameKey = "ClassName";
        public const string DayOfWeekKey = "DayOfWeek";
        public const string TimeKey = "Time";

        public static BookingJobData Parse(IDictionary<string, string> data)
        {
            string className = GetRequired(data, ClassNameKey);

            if (!Enum.TryParse(GetRequired(data, DayOfWeekKey), true, out DayOfWeek dayOfWeek))
            {
                throw new FormatException($"Invalid {DayOfWeekKey} value '{data[DayOfWeekKey]}'");
            }

            TimeOnly time = TimeOnly.Parse(GetRequired(data, TimeKey), CultureInfo.InvariantCulture);

            return new BookingJobData(className, dayOfWeek, time);
        }

        private static string GetRequired(IDictionary<string, string> data, string key) =>
            data.TryGetValue(key, out string? value) && !value.IsNullOrBlankString()
                ? value
                : throw new InvalidOperationException($"Missing required job data '{key}'");
    }
}
