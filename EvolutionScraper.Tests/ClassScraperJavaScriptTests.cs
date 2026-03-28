using Microsoft.Extensions.Configuration;
using PuppeteerSharp;
using System.Text.Json;
using Xunit;

namespace EvolutionScraper.Tests;

public sealed class ClassScraperJavaScriptTests : IAsyncDisposable
{
    private IBrowser? _browser;
    private IPage? _page;
    private readonly string _htmlPath;
    private readonly string _scriptPath;

    private readonly IConfiguration _configuration;

    public ClassScraperJavaScriptTests()
    {
        string testDataDir = Path.Combine(AppContext.BaseDirectory, "TestData");
        _htmlPath = Path.Combine(testDataDir, "page_dump.dat");
        _scriptPath = Path.Combine(testDataDir, "class_selector.js");

        _configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    private async Task InitializeBrowserAsync()
    {
        if (_browser is null)
        {
            string chromePath = _configuration.GetSection("EvolutionScraperOptions:ChromePath").Value
                ?? throw new InvalidOperationException("ChromePath not configured in appsettings.json");

            LaunchOptions launchOptions = new()
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox"],
                ExecutablePath = chromePath
            };
            _browser = await Puppeteer.LaunchAsync(launchOptions);
            _page = await _browser.NewPageAsync();
        }
    }

    [Fact]
    public async Task JavaScriptScraper_ShouldReturnValidResults()
    {
        await InitializeBrowserAsync();

        // Load the HTML page dump
        string htmlContent = await File.ReadAllTextAsync(_htmlPath, TestContext.Current.CancellationToken);
        await _page!.SetContentAsync(htmlContent);

        // Load and execute the JavaScript scraper
        string script = await File.ReadAllTextAsync(_scriptPath, TestContext.Current.CancellationToken);

        // Execute the scraper - it returns an array directly
        JsonDocument o = await _page.EvaluateFunctionAsync<JsonDocument>(script);

        // Deserialize results
        List<ClassScheduleItem> items = JsonSerializer.Deserialize<List<ClassScheduleItem>>(o.RootElement.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];

        Assert.NotNull(items);
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.NotNull(item.Instructor);
            Assert.NotEmpty(item.Instructor);
            Assert.True(item.Date != default);
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null)
        {
            if (!_page.IsClosed)
            {
                await _page.CloseAsync();
            }
            await _page.DisposeAsync();
        }

        if (_browser is not null)
        {
            if (!_browser.IsClosed)
            {
                await _browser.CloseAsync();
            }
            await _browser.DisposeAsync();
        }
    }
}
