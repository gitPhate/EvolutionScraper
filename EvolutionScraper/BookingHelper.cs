using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EvolutionScraper.Tests")]

namespace EvolutionScraper
{
    public static class BookingHelper
    {
        public static bool IsBookingDay(DayOfWeek targetDay, DateTime today)
        {
            DateTime nextTargetDate = GetNextOccurrence(today, targetDay);
            DateTime idealBookingDate = nextTargetDate.AddDays(-3);

            // If the ideal booking date and the class are in different months,
            // the gym server blocks bookings until the 1st of the target month
            if (idealBookingDate.Month != nextTargetDate.Month)
            {
                DateTime firstOfTargetMonth = new(nextTargetDate.Year, nextTargetDate.Month, 1);
                return today.Date == firstOfTargetMonth.Date;
            }

            // Same month: book exactly 3 days before the class
            return today.Date == idealBookingDate.Date;
        }

        internal static DateTime GetNextOccurrence(DateTime startDate, DayOfWeek targetDay)
        {
            DateTime current = startDate;
            do
            {
                current = current.AddDays(1);
            }
            while (current.DayOfWeek != targetDay);
            return current;
        }
    }
}
