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

    internal class EvolutionScraper(EvolutionScraperOptions options, ILogger<EvolutionScraper> logger) : IDisposable
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
                Args = ["--no-sandbox", "--disable-setuid-sandbox"],
                ExecutablePath = _options.ChromePath
            };

            _browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
            _page = await _browser.NewPageAsync().ConfigureAwait(false);

            _jsonOptions.Converters.Add(new DateTimeConverter());
        }

        private async Task LoginAsync()
        {
            // Set a realistic user agent
            await _page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36").ConfigureAwait(false);

            // Navigate to login page
            await _page.GoToAsync("https://clients.mindbodyonline.com/ASP/su1.asp?studioid=531524&tg=&vt=&lvl=&stype=&view=&trn=0&page=&catid=&prodid=&date=9%2f28%2f2025&classid=0&prodGroupId=&sSU=&optForwardingLink=&qParam=&justloggedin=&nLgIn=&pMode=0&loc=1",
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] })
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
                throw new InvalidOperationException("Unable to login");
            }
        }

        private async Task FindClassesPageAsync()
        {
            bool isWeekend =
                DateTime.Today.DayOfWeek == DayOfWeek.Saturday
                || DateTime.Today.DayOfWeek == DayOfWeek.Sunday;

            if (!isWeekend)
            {
                await WaitUntilDueHourAsync(9, 2).ConfigureAwait(false);
            }

            await _page.ClickAsync(".tab-c-firstTab > a").ConfigureAwait(false);
            await _page.WaitAsync().ConfigureAwait(false);

            if (isWeekend)
            {
                await WaitUntilDueHourAsync(9, 2).ConfigureAwait(false);

                await _page.ClickAsync("#day-arrow-r").ConfigureAwait(false);
                await _page.WaitAsync().ConfigureAwait(false);
            }
        }

        private async ValueTask WaitUntilDueHourAsync(int hour, int maxMinutesToWait)
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
        }

        private async Task<ClassScheduleItem[]> ScrapeClassSchedulesAsync()
        {
            string script = File.ReadAllText("class_selector.js");
            JsonDocument o = await _page.EvaluateFunctionAsync<JsonDocument>(script).ConfigureAwait(false);
            List<ClassScheduleItem> items = JsonSerializer.Deserialize<List<ClassScheduleItem>>(o.RootElement.GetRawText(), _jsonOptions) ?? [];
            return items.ToArray();
        }

        internal async Task<bool> BookClassAsync(string className, DayOfWeek day, TimeOnly time)
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

            try
            {
                await FindClassesPageAsync().ConfigureAwait(false);
            }
            catch (NotSupportedException ex)
            {
                logger.LogInformation(ex.Message);
                return false;
            }

            ClassScheduleItem[] items = await ScrapeClassSchedulesAsync().ConfigureAwait(false);

            ClassScheduleItem? classToBook =
                items
                    .FirstOrDefault(x => x.ClassName.ToLowerInvariant() == className.ToLowerInvariant()
                        && x.Date == date);

            if (classToBook is null)
            {
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

        public void Dispose()
        {
            _page?.Dispose();
            _browser?.Dispose();
        }
    }
}
