using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EvolutionScraper.Tests;

/// <summary>
/// Drives the real browser against the real gym website and books a class.
/// WARNING: this creates an actual booking. It is skipped unless credentials are set in the
/// test project's <c>.env</c> file (see <c>.env.example</c>) or in the process environment.
/// </summary>
[Trait("Category", "EndToEnd")]
public sealed class EvolutionScraperEndToEndTests
{
    private const string ClassName = "TotalBody";
    private const DayOfWeek Day = DayOfWeek.Monday;
    private static readonly TimeOnly Time = new(7, 30);
    private const string SecondClassName = "Pilates";
    private static readonly TimeOnly SecondTime = new(8, 30);

    static EvolutionScraperEndToEndTests()
    {
        // Existing process environment variables win over the file
        DotNetEnv.Env.TraversePath().NoClobber().Load(Path.Combine(AppContext.BaseDirectory, ".env"));
    }

    [Fact]
    public async Task BookClassAsync_TotalBodyOnMondayAt0730_BooksTheClass()
    {
        string username = DotNetEnv.Env.GetString("EVOLUTION_USERNAME");
        string password = DotNetEnv.Env.GetString("EVOLUTION_PASSWORD");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password),
            "Set EVOLUTION_USERNAME and EVOLUTION_PASSWORD in .env to run the end-to-end test.");

        string chromePath = DotNetEnv.Env.GetString("EVOLUTION_CHROME_PATH", GetConfiguredChromePath());
        EvolutionScraperOptions options = new(chromePath, username, password, false);

        await using EvolutionScraper scraper = new(options, NullLogger.Instance);

        bool isBooked = await scraper.BookClassAsync(ClassName, Day, Time);

        Assert.True(isBooked, $"Expected {ClassName} on {Day} at {Time} to be booked.");
    }

    [Fact]
    public async Task BookClassAsync_TwoClassesWithSameInstance_BooksBoth()
    {
        string username = DotNetEnv.Env.GetString("EVOLUTION_USERNAME");
        string password = DotNetEnv.Env.GetString("EVOLUTION_PASSWORD");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password),
            "Set EVOLUTION_USERNAME and EVOLUTION_PASSWORD in .env to run the end-to-end test.");

        string chromePath = DotNetEnv.Env.GetString("EVOLUTION_CHROME_PATH", GetConfiguredChromePath());
        EvolutionScraperOptions options = new(chromePath, username, password, false);

        await using EvolutionScraper scraper = new(options, NullLogger.Instance);

        // The first call launches the browser and logs in; the second must reuse the same session
        bool isFirstBooked = await scraper.BookClassAsync(ClassName, Day, Time);
        bool isSecondBooked = await scraper.BookClassAsync(SecondClassName, Day, SecondTime);

        Assert.True(isFirstBooked, $"Expected {ClassName} on {Day} at {Time} to be booked.");
        Assert.True(isSecondBooked, $"Expected {SecondClassName} on {Day} at {SecondTime} to be booked.");
    }

    private static string GetConfiguredChromePath()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        return configuration["EvolutionScraperOptions:ChromePath"] ?? string.Empty;
    }
}
