using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System.Text.Json;

namespace EvolutionScraper
{
    public record EvolutionScraperOptions(string ChromePath, string Username, string Password)
    {
        public EvolutionScraperOptions() : this(string.Empty, string.Empty, string.Empty)
        {
        }
    }

    public sealed class EvolutionScraper(EvolutionScraperOptions options, ILogger logger) : IDisposable
    {
        private readonly EvolutionScraperOptions _options = options;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        private IBrowser _browser = null!;
        private IPage _page = null!;

        private async Task RunBrowserAsync()
        {
            // Download and initialize browser
            LaunchOptions launchOptions = new()
            {
                Headless = true,
                Args = [
                    "--disable-blink-features=AutomationControlled",
                    "--disable-dev-shm-usage",
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-infobars",
                    "--window-size=1920,1080",
                    "--start-maximized",
                    "--disable-extensions",
                ],
                IgnoredDefaultArgs = ["--enable-automation"],
                ExecutablePath = _options.ChromePath
            };

            _browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
            _page = await _browser.NewPageAsync().ConfigureAwait(false);

            _jsonOptions.Converters.Add(new DateTimeConverter());
        }

        private async Task LoginAsync()
        {
            await _page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 }).ConfigureAwait(false);

            await _page.EvaluateExpressionOnNewDocumentAsync(@"
    Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
    Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
    Object.defineProperty(navigator, 'languages', { get: () => ['en-US', 'en'] });
    window.chrome = { runtime: {} };
    Object.defineProperty(navigator, 'permissions', {
        query: (parameters) => (
            parameters.name === 'notifications'
                ? Promise.resolve({ state: Notification.permission })
                : navigator.permissions.query(parameters)
        )
    });
").ConfigureAwait(false);

            // Set a realistic user agent
            await _page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36").ConfigureAwait(false);

            await _page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
            {
                ["accept-language"] = "en-US,en;q=0.9",
                ["accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
                ["sec-ch-ua"] = "\"Not:A-Brand\";v=\"99\", \"Google Chrome\";v=\"145\", \"Chromium\";v=\"145\"",
                ["sec-ch-ua-mobile"] = "?0",
                ["sec-ch-ua-platform"] = "\"Windows\"",
                ["upgrade-insecure-requests"] = "1"
            })
            .ConfigureAwait(false);

            //Restore cookies

            string currentUrlDate = DateTime.Today.ToString("M/d/yyyy").Replace("/", "%2f");
            // Navigate to login page
            await _page.GoToAsync($"https://clients.mindbodyonline.com/ASP/su1.asp?studioid=531524&tg=&vt=&lvl=&stype=&view=&trn=0&page=&catid=&prodid=&date={currentUrlDate}&classid=0&prodGroupId=&sSU=&optForwardingLink=&qParam=&justloggedin=&nLgIn=&pMode=0&loc=1",
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded] })
                .ConfigureAwait(false);

            // Wait for login form to load
            await _page.WaitForSelectorAsync("#su1UserName").ConfigureAwait(false);
            await _page.WaitForSelectorAsync("#su1Password").ConfigureAwait(false);

            // Fill credentials
            await _page.TypeAsync("#su1UserName", _options.Username, new TypeOptions { Delay = 100 }).ConfigureAwait(false);
            await _page.TypeAsync("#su1Password", _options.Password, new TypeOptions { Delay = 100 }).ConfigureAwait(false);

            // Submit form and wait for navigation
            await _page.ClickAsync("#btnSu1Login").ConfigureAwait(false);
            await _page.WaitAsync().ConfigureAwait(false);

            // Verify successful login
            bool isLoggedIn = await _page.EvaluateExpressionAsync<bool>(
                "document.querySelector('#myInfoContainer') !== null")
                .ConfigureAwait(false);

            if (!isLoggedIn)
            {
                await ThrowLoggingPageAsync(new InvalidOperationException("Unable to login")).ConfigureAwait(false);
            }
        }

        private async Task FindClassesPageAsync()
        {
            bool isWeekend =
                DateTime.Today.DayOfWeek == DayOfWeek.Saturday
                || DateTime.Today.DayOfWeek == DayOfWeek.Sunday;

            if (!isWeekend)
            {
                await WaitUntilDueTimeAsync(9, 5, 60).ConfigureAwait(false);
            }

            await _page.ClickAsync(".tab-c-firstTab > a").ConfigureAwait(false);
            await _page.WaitAsync().ConfigureAwait(false);

            if (isWeekend)
            {
                await WaitUntilDueTimeAsync(9, 5, 60).ConfigureAwait(false);

                await _page.ClickAsync("#day-arrow-r").ConfigureAwait(false);
                await _page.WaitAsync().ConfigureAwait(false);
            }
        }

        private async ValueTask WaitUntilDueTimeAsync(short hour, short maxMinutesToWait, short secondsToWaitAfterDueTime)
        {
            if (DateTime.Now.Hour != hour - 1
                || (60 - DateTime.Now.Minute > maxMinutesToWait))
            {
                throw new NotSupportedException($"Current time is past or too far from the due hour ({DateTime.Now})");
            }

            while (DateTime.Now.Hour != hour)
            {
                logger.LogInformation($"Waiting for the right time ({DateTime.Now})");
                await Task.Delay(1000).ConfigureAwait(false);
            }

            await Task.Delay(secondsToWaitAfterDueTime * 1000).ConfigureAwait(false);
        }

        private async Task<ClassScheduleItem[]> ScrapeClassSchedulesAsync()
        {
            string script = File.ReadAllText("class_selector.js");
            JsonDocument o = await _page.EvaluateFunctionAsync<JsonDocument>(script).ConfigureAwait(false);
            List<ClassScheduleItem> items = JsonSerializer.Deserialize<List<ClassScheduleItem>>(o.RootElement.GetRawText(), _jsonOptions) ?? [];
            return items.ToArray();
        }

        public async Task<bool> BookClassAsync(string className, DayOfWeek day, TimeOnly time)
        {
            try
            {
                return await BookClassCoreAsync(className, day, time).ConfigureAwait(false);
            }
            finally
            {
                await _page.CloseAsync().ConfigureAwait(false);
                await _browser.CloseAsync().ConfigureAwait(false);
            }
        }

        private async Task<bool> BookClassCoreAsync(string className, DayOfWeek day, TimeOnly time)
        {
            DateTime date = Extensions.GetNextDateTime(day, time);

            await RunBrowserAsync().ConfigureAwait(false);
            await LoginAsync().ConfigureAwait(false);
            await FindClassesPageAsync().ConfigureAwait(false);

            ClassScheduleItem[] items = await ScrapeClassSchedulesAsync().ConfigureAwait(false);

            ClassScheduleItem? classToBook =
                items
                    .FirstOrDefault(x => x.ClassName.ToLowerInvariant() == className.ToLowerInvariant()
                        && x.Date == date);

            if (classToBook is null)
            {
                logger.LogDebug("All classes scraped:");
                foreach (ClassScheduleItem item in items)
                {
                    logger.LogDebug(JsonSerializer.Serialize(item, _jsonOptions));
                }

                await ThrowLoggingPageAsync(new InvalidOperationException("Unable to find any class to book")).ConfigureAwait(false);
                return false;
            }

            await _page.ClickAsync($"input[name=\"{classToBook.Button}\"]").ConfigureAwait(false);
            await _page.WaitAsync().ConfigureAwait(false);
            await _page.ClickAsync($"#SubmitEnroll2").ConfigureAwait(false);
            await _page.WaitAsync().ConfigureAwait(false);

            bool isBooked = await _page.EvaluateExpressionAsync<bool>(
                "document.querySelector('#notifyBooking') !== null")
                .ConfigureAwait(false);

            return isBooked;
        }

        private async Task ThrowLoggingPageAsync(Exception ex)
        {
            string content = await _page.GetContentAsync().ConfigureAwait(false);
            await File.WriteAllTextAsync($"page_dump_{DateTime.Now:yyyyMMddHHmmss}.html", content).ConfigureAwait(false);
            throw ex;
        }

        public void Dispose()
        {
            _page?.Dispose();
            _browser?.Dispose();
        }
    }
}
