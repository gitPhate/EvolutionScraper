using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PuppeteerSharp;

namespace EvolutionScraper.Tests;

/// <summary>
/// Verifies that BookClassAsync only performs the full browser launch + login on the
/// first invocation and reuses the existing session (GoToMainPageAsync) on subsequent
/// calls from the same instance.
/// </summary>
public sealed class EvolutionScraperLoginTests
{
    private const string TestClassName = "Yoga";
    private static readonly DayOfWeek TestDay = DayOfWeek.Monday;
    private static readonly TimeOnly TestTime = new(10, 0);

    [Fact]
    public async Task BookClassAsync_FirstCall_InitializesBrowserAndLogsIn()
    {
        var scraper = new TrackingEvolutionScraper();

        await scraper.BookClassAsync(TestClassName, TestDay, TestTime);

        Assert.Equal(1, scraper.RunBrowserCallCount);
        Assert.Equal(1, scraper.LoginCallCount);
        Assert.Equal(0, scraper.GoToMainPageCallCount);
    }

    [Fact]
    public async Task BookClassAsync_SecondCall_ReusesSessionWithoutLoggingInAgain()
    {
        var scraper = new TrackingEvolutionScraper();

        await scraper.BookClassAsync(TestClassName, TestDay, TestTime);
        await scraper.BookClassAsync(TestClassName, TestDay, TestTime);

        Assert.Equal(1, scraper.RunBrowserCallCount);
        Assert.Equal(1, scraper.LoginCallCount);
        Assert.Equal(1, scraper.GoToMainPageCallCount);
    }

    [Fact]
    public async Task BookClassAsync_MultipleSubsequentCalls_ReusesSessionEachTime()
    {
        var scraper = new TrackingEvolutionScraper();

        await scraper.BookClassAsync(TestClassName, TestDay, TestTime);
        await scraper.BookClassAsync(TestClassName, TestDay, TestTime);
        await scraper.BookClassAsync(TestClassName, TestDay, TestTime);

        Assert.Equal(1, scraper.RunBrowserCallCount);
        Assert.Equal(1, scraper.LoginCallCount);
        Assert.Equal(2, scraper.GoToMainPageCallCount);
    }

    /// <summary>
    /// A testable subclass that stubs out all browser I/O and tracks which
    /// initialization methods are called on each BookClassAsync invocation.
    /// </summary>
    private sealed class TrackingEvolutionScraper : EvolutionScraper
    {
        public int RunBrowserCallCount { get; private set; }
        public int LoginCallCount { get; private set; }
        public int GoToMainPageCallCount { get; private set; }

        public TrackingEvolutionScraper()
            : base(new EvolutionScraperOptions(), NullLogger.Instance)
        {
        }

        protected override Task RunBrowserAsync()
        {
            RunBrowserCallCount++;
            // Provide a fake IPage so the null-guard in BookClassAsync passes
            // and subsequent calls take the GoToMainPageAsync path.
            _page = Substitute.For<IPage>();
            return Task.CompletedTask;
        }

        protected override Task LoginAsync()
        {
            LoginCallCount++;
            return Task.CompletedTask;
        }

        protected override Task GoToMainPageAsync()
        {
            GoToMainPageCallCount++;
            return Task.CompletedTask;
        }

        protected override Task FindClassesPageAsync(bool shouldGoToNextWeek) =>
            Task.CompletedTask;

        protected override Task<ClassScheduleItem[]> ScrapeClassSchedulesAsync() =>
            Task.FromResult<ClassScheduleItem[]>(
            [
                new ClassScheduleItem
                {
                    ClassName = "Yoga",
                    Button = "btn-yoga",
                    Date = Extensions.GetNextDateTime(DayOfWeek.Monday, new TimeOnly(10, 0))
                }
            ]);
    }
}
