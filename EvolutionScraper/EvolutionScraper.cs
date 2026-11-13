using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using System.Diagnostics;
using System.Text.Json;

namespace EvolutionScraper
{
    public record EvolutionScraperOptions(string ChromePath, string Username, string Password, bool HeadlessBrowser = true)
    {
        public EvolutionScraperOptions() : this(string.Empty, string.Empty, string.Empty)
        {
        }
    }

    public class EvolutionScraper(EvolutionScraperOptions options, ILogger logger, string? instanceName = null) : IDisposable, IAsyncDisposable
    {
        const string MainPageUrl = "https://clients.mindbodyonline.com/consumermyinfo?studioid=531524";

        private readonly EvolutionScraperOptions _options = options;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        private IBrowser? _browser = null!;
        protected IPage? _page = null!;

        private bool _isOnNextWeek = false;

        protected virtual async Task RunBrowserAsync()
        {
            // Download and initialize browser
            LaunchOptions launchOptions = new()
            {
                Headless = _options.HeadlessBrowser,
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

            // Do not use SetExtraHttpHeadersAsync here: it applies to every request, including cross-origin fetch calls.
            // Non-safelisted headers (upgrade-insecure-requests, sec-ch-ua-*) force a CORS preflight that the new
            // mindbody pages' static-content host rejects, so the page never renders.
        }

        protected virtual async Task LoginAsync()
        {
            if (_page is null)
            {
                throw new InvalidOperationException("Browser is not initialized");
            }

            // Navigate to the landing page, which shows the "Accedi" button when logged out
            await _page.GoToAsync(MainPageUrl,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] })
                .ConfigureAwait(false);

            await _page.ClickAsync(".myInfoSignInButton", new ClickOptions { Delay = 100 }).ConfigureAwait(false);
            await _page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded] }).ConfigureAwait(false);

            // Email step on signin.mindbodyonline.com
            await _page.WaitForSelectorAsync("#username").ConfigureAwait(false);
            await _page.TypeAsync("#username", _options.Username, new TypeOptions { Delay = 100 }).ConfigureAwait(false);
            await _page.ClickAsync("button[type=submit]", new ClickOptions { Delay = 100 }).ConfigureAwait(false);

            // Password step
            await _page.WaitForSelectorAsync("#password").ConfigureAwait(false);
            await _page.TypeAsync("#password", _options.Password, new TypeOptions { Delay = 100 }).ConfigureAwait(false);
            await _page.ClickAsync("button[type=submit]", new ClickOptions { Delay = 100 }).ConfigureAwait(false);

            // Sign-in goes through several redirects before landing back on consumermyinfo
            await _page.WaitForSelectorAsync("#accountFlydown", new WaitForSelectorOptions { Timeout = 60000 }).ConfigureAwait(false);

            await VerifySuccessulLoginAsync().ConfigureAwait(false);
        }

        private async Task VerifySuccessulLoginAsync()
        {
            if (_page is null)
            {
                throw new InvalidOperationException("Browser is not initialized");
            }

            // Verify successful login
            bool isLoggedIn = await _page.EvaluateExpressionAsync<bool>(
                "document.querySelector('#accountFlydown') !== null")
                .ConfigureAwait(false);

            if (!isLoggedIn)
            {
                await ThrowLoggingPageAsync(new InvalidOperationException("Unable to login")).ConfigureAwait(false);
            }
        }

        protected virtual async Task FindClassesPageAsync(bool shouldGoToNextWeek)
        {
            if (_page is null)
            {
                throw new InvalidOperationException("Browser is not initialized");
            }

            if (!shouldGoToNextWeek)
            {
                await WaitUntilDueTimeAsync(9, 5, 120).ConfigureAwait(false);
            }

            await _page.ClickAsync("li.tabclass > .tab-c > a").ConfigureAwait(false);
            await _page.WaitAsync().ConfigureAwait(false);

            // The calendar remembers the last displayed week, so align it with the wanted one.
            if (shouldGoToNextWeek != _isOnNextWeek)
            {
                if (shouldGoToNextWeek)
                {
                    await WaitUntilDueTimeAsync(9, 5, 120).ConfigureAwait(false);
                }

                await _page.ClickAsync(shouldGoToNextWeek ? "#week-arrow-r" : "#week-arrow-l").ConfigureAwait(false);
                await _page.WaitAsync().ConfigureAwait(false);
                _isOnNextWeek = shouldGoToNextWeek;
            }
        }

        private async ValueTask WaitUntilDueTimeAsync(short hour, short maxMinutesToWait, short secondsToWaitAfterDueTime)
        {
            if (Debugger.IsAttached || DateTime.Now.Hour >= hour)
            {
                return;
            }

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

            logger.LogInformation($"Waiting extra {secondsToWaitAfterDueTime} seconds after due time");
            await Task.Delay(secondsToWaitAfterDueTime * 1000).ConfigureAwait(false);
        }

        protected virtual async Task<ClassScheduleItem[]> ScrapeClassSchedulesAsync()
        {
            if (_page is null)
            {
                throw new InvalidOperationException("Browser is not initialized");
            }

            string script = File.ReadAllText("class_selector.js");
            JsonDocument o = await _page.EvaluateFunctionAsync<JsonDocument>(script).ConfigureAwait(false);
            List<ClassScheduleItem> items = JsonSerializer.Deserialize<List<ClassScheduleItem>>(o.RootElement.GetRawText(), _jsonOptions) ?? [];
            return items.ToArray();
        }

        public async Task<bool> BookClassAsync(string className, DayOfWeek day, TimeOnly time)
        {
            try
            {
                if (_page is null)
                {
                    await RunBrowserAsync().ConfigureAwait(false);

                    if (_page is null)
                    {
                        throw new InvalidOperationException("Browser is not initialized");
                    }

                    try
                    {
                        await LoginAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        await ThrowLoggingPageAsync(ex).ConfigureAwait(false);
                    }
                }
                else
                {
                    await GoToMainPageAsync().ConfigureAwait(false);
                }

                bool shouldGoToNextWeek = BookingHelper.IsBookingDayNextWeek(day, DateTime.Today);

                await FindClassesPageAsync(shouldGoToNextWeek).ConfigureAwait(false);

                ClassScheduleItem[] items = await ScrapeClassSchedulesAsync().ConfigureAwait(false);

                ClassScheduleItem? classToBook =
                    items
                        .FirstOrDefault(x => x.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase)
                            && x.Date == Extensions.GetNextDateTime(day, time));

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
            catch (Exception ex)
            {
                await ThrowLoggingPageAsync(ex).ConfigureAwait(false);
                throw;
            }
        }

        protected virtual async Task GoToMainPageAsync()
        {
            if (_page is null)
            {
                throw new InvalidOperationException("Browser is not initialized");
            }

            await _page.GoToAsync(MainPageUrl,
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.DOMContentLoaded] }).ConfigureAwait(false);
            await VerifySuccessulLoginAsync().ConfigureAwait(false);
        }

        private async Task ThrowLoggingPageAsync(Exception ex)
        {
            if (_page is null)
            {
                throw ex;
            }

            string content = await _page.GetContentAsync().ConfigureAwait(false);
            // The instance name keeps dumps of parallel bookings failing in the same second from overwriting each other
            string suffix = string.IsNullOrWhiteSpace(instanceName)
                ? string.Empty
                : $"_{string.Concat(instanceName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))}";
            await File.WriteAllTextAsync($"page_dump_{DateTime.Now:yyyyMMddHHmmss}{suffix}.html", content).ConfigureAwait(false);
            throw ex;
        }

        public void Dispose()
        {
            if (_page is not null)
            {
                if (!_page.IsClosed)
                {
                    _page.CloseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }

                _page.Dispose();
            }

            if (_browser is not null)
            {
                if (!_browser.IsClosed)
                {
                    _browser.CloseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                }

                _browser.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_page is not null)
            {
                if (!_page.IsClosed)
                {
                    await _page.CloseAsync().ConfigureAwait(false);
                }

                await _page.DisposeAsync().ConfigureAwait(false);
            }

            if (_browser is not null)
            {
                if (!_browser.IsClosed)
                {
                    await _browser.CloseAsync().ConfigureAwait(false);
                }

                await _browser.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
