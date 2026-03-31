namespace EvolutionScraper.Tests;

public sealed class BookingHelperTests
{
    // Same month: exactly 3 days before the class → should book
    [Fact]
    public void IsBookingDay_SameMonth_ExactlyThreeDaysBefore_ShouldReturnTrue()
    {
        // March 23 (Mon) → March 26 (Thu), same month, 3 days apart
        Assert.True(BookingHelper.IsBookingDay(DayOfWeek.Thursday, new DateTime(2026, 3, 23)));
    }

    // Same month: not exactly 3 days before → should not book
    [Fact]
    public void IsBookingDay_SameMonth_NotThreeDaysBefore_ShouldReturnFalse()
    {
        // March 24 (Tue) → March 26 (Thu), only 2 days apart
        Assert.False(BookingHelper.IsBookingDay(DayOfWeek.Thursday, new DateTime(2026, 3, 24)));
    }

    // Cross-month: today is the 1st of the target month → should book (gym API unlocks)
    [Fact]
    public void IsBookingDay_CrossMonth_OnFirstOfTargetMonth_ShouldReturnTrue()
    {
        // April 1 (Wed) → April 2 (Thu), ideal date (Mar 30) was in a different month
        Assert.True(BookingHelper.IsBookingDay(DayOfWeek.Thursday, new DateTime(2026, 4, 1)));
    }

    // Cross-month: today is before the 1st of the target month → should not book
    [Fact]
    public void IsBookingDay_CrossMonth_BeforeFirstOfTargetMonth_ShouldReturnFalse()
    {
        // March 30 (Mon) → April 2 (Thu), gym blocks bookings until April 1
        Assert.False(BookingHelper.IsBookingDay(DayOfWeek.Thursday, new DateTime(2026, 3, 30)));
    }

    // GetNextOccurrence: always skips startDate and returns the next future occurrence
    [Fact]
    public void GetNextOccurrence_ReturnsNextFutureOccurrence()
    {
        // March 30 (Mon) → next Thursday is April 2 (crosses month boundary)
        Assert.Equal(new DateTime(2026, 4, 2), BookingHelper.GetNextOccurrence(new DateTime(2026, 3, 30), DayOfWeek.Thursday));
    }
}


