using EvolutionScraper.Service.Support.Quartz;

namespace EvolutionScraper.Tests;

public sealed class BookingDayCalendarTests
{
    private static BookingDayCalendar CreateCalendar(DayOfWeek day) => new() { BookedDay = day };

    private static DateTimeOffset At0859(int year, int month, int day) =>
        new(new DateTime(year, month, day, 8, 59, 0, DateTimeKind.Local));

    private static DateTime LocalDate(BookingDayCalendar calendar, DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, calendar.TimeZone).Date;

    // Wednesday March 25 2026 → booking opens Sunday March 22
    [Fact]
    public void IsTimeIncluded_ThreeDaysBeforeClass_ShouldReturnTrue()
    {
        Assert.True(CreateCalendar(DayOfWeek.Wednesday).IsTimeIncluded(At0859(2026, 3, 22)));
    }

    [Fact]
    public void IsTimeIncluded_OtherDays_ShouldReturnFalse()
    {
        BookingDayCalendar calendar = CreateCalendar(DayOfWeek.Wednesday);

        Assert.False(calendar.IsTimeIncluded(At0859(2026, 3, 23)));
        Assert.False(calendar.IsTimeIncluded(At0859(2026, 3, 25)));
    }

    // Wednesday December 2 2026 → Sunday November 29 is in the previous month, so booking opens on December 1
    [Fact]
    public void IsTimeIncluded_CrossMonth_OnFirstOfTargetMonth_ShouldReturnTrue()
    {
        Assert.True(CreateCalendar(DayOfWeek.Wednesday).IsTimeIncluded(At0859(2026, 12, 1)));
    }

    [Fact]
    public void IsTimeIncluded_CrossMonth_ThreeDaysBeforeInPreviousMonth_ShouldReturnFalse()
    {
        Assert.False(CreateCalendar(DayOfWeek.Wednesday).IsTimeIncluded(At0859(2026, 11, 29)));
    }

    [Fact]
    public void GetNextIncludedTimeUtc_SkipsExcludedDays()
    {
        BookingDayCalendar calendar = CreateCalendar(DayOfWeek.Wednesday);

        // November 28 → November 29 is excluded by the month rule → next valid day is December 1
        DateTimeOffset next = calendar.GetNextIncludedTimeUtc(At0859(2026, 11, 28));

        Assert.Equal(new DateTime(2026, 12, 1), LocalDate(calendar, next));
    }

    [Fact]
    public void GetNextIncludedTimeUtc_OnIncludedDay_ReturnsSameTime()
    {
        BookingDayCalendar calendar = CreateCalendar(DayOfWeek.Wednesday);
        DateTimeOffset time = At0859(2026, 3, 22);

        Assert.Equal(time, calendar.GetNextIncludedTimeUtc(time));
    }
}

public sealed class BookingRetryTests
{
    [Theory]
    [InlineData(0, 3, true)]
    [InlineData(2, 3, true)]
    [InlineData(3, 3, false)]
    [InlineData(0, 0, false)]
    public void CanRetry_ShouldStopAfterMaxRetries(int retriesDone, int maxRetries, bool expected)
    {
        Assert.Equal(expected, BookingRetry.CanRetry(retriesDone, maxRetries));
    }

    [Fact]
    public void GetRetryTriggerName_ShouldBeUniquePerRetry()
    {
        Assert.NotEqual(
            BookingRetry.GetRetryTriggerName("Calisthenics-Wednesday", 1),
            BookingRetry.GetRetryTriggerName("Calisthenics-Wednesday", 2));
    }
}

public sealed class BookingJobDataTests
{
    [Fact]
    public void Parse_ValidData_ShouldReturnBooking()
    {
        BookingJobData data = BookingJobData.Parse(new Dictionary<string, string>
        {
            ["ClassName"] = "Calisthenics",
            ["DayOfWeek"] = "Wednesday",
            ["Time"] = "20:00:00"
        });

        Assert.Equal(new BookingJobData("Calisthenics", DayOfWeek.Wednesday, new TimeOnly(20, 0)), data);
    }

    [Fact]
    public void Parse_MissingKey_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => BookingJobData.Parse(new Dictionary<string, string>
        {
            ["ClassName"] = "Calisthenics",
            ["Time"] = "20:00:00"
        }));
    }

    [Fact]
    public void Parse_InvalidDay_ShouldThrow()
    {
        Assert.Throws<FormatException>(() => BookingJobData.Parse(new Dictionary<string, string>
        {
            ["ClassName"] = "Calisthenics",
            ["DayOfWeek"] = "Funday",
            ["Time"] = "20:00:00"
        }));
    }
}
