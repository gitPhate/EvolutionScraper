using Quartz.Impl.Calendar;

namespace EvolutionScraper.Service.Support.Quartz
{
    /// <summary>
    /// Includes only the days on which the class of <see cref="BookedDay"/> can be booked
    /// (3 days before it, or the 1st of the month when that falls in the previous month).
    /// </summary>
    internal sealed class BookingDayCalendar : BaseCalendar
    {
        public const string Name = "BookingDay";

        public DayOfWeek BookedDay { get; set; }

        public override bool IsTimeIncluded(DateTimeOffset timeUtc)
        {
            if (!base.IsTimeIncluded(timeUtc))
            {
                return false;
            }

            DateTime localDate = TimeZoneInfo.ConvertTime(timeUtc, TimeZone).Date;
            return BookingHelper.IsBookingDay(BookedDay, localDate);
        }

        public override DateTimeOffset GetNextIncludedTimeUtc(DateTimeOffset timeUtc)
        {
            DateTimeOffset candidate = timeUtc;

            // The booking rule repeats at most every month, so a year is always enough to find a valid day
            for (int i = 0; i < 400 && !IsTimeIncluded(candidate); i++)
            {
                DateTime nextMidnight = TimeZoneInfo.ConvertTime(candidate, TimeZone).Date.AddDays(1);
                candidate = new DateTimeOffset(nextMidnight, TimeZone.GetUtcOffset(nextMidnight));
            }

            return candidate;
        }
    }
}
