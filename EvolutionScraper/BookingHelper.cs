using System.Globalization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("EvolutionScraper.Tests")]

namespace EvolutionScraper
{
    public static class BookingHelper
    {
        public static bool IsBookingDay(DayOfWeek targetDay, DateTime today)
        {
            DateTime nextTargetDate = Extensions.GetNextDateTime(targetDay, today);
            DateTime idealBookingDate = nextTargetDate.AddDays(-3);

            // If the ideal booking date and the class are in different months,
            // the gym server blocks bookings until the 1st of the target month
            if (idealBookingDate.Month != nextTargetDate.Month)
            {
                idealBookingDate = new(nextTargetDate.Year, nextTargetDate.Month, 1);
            }

            // Same month: book exactly 3 days before the class
            return today.Date == idealBookingDate.Date;
        }

        internal static bool IsBookingDayNextWeek(DayOfWeek targetDay, DateTime startDate)
        {
            DateTime bookingDay = Extensions.GetNextDateTime(targetDay, startDate);
            return ISOWeek.GetWeekOfYear(startDate) != ISOWeek.GetWeekOfYear(bookingDay);
        }
    }
}
