using PuppeteerSharp;
using PuppeteerSharp.Input;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvolutionAutomation
{
    public record EvolutionScraperOptions(string ChromePath, string Username, string Password);

    internal class EvolutionScraper(EvolutionScraperOptions options)
    {
        internal async Task<IPage> GetMainPageAsync()
        {
            // Download and initialize browser
            LaunchOptions launchOptions = new()
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox"],
                ExecutablePath = options.ChromePath
            };

            using IBrowser browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
            using IPage page = await browser.NewPageAsync().ConfigureAwait(false);

            // Set a realistic user agent
            await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36").ConfigureAwait(false);

            // Navigate to login page
            await page.GoToAsync("https://clients.mindbodyonline.com/ASP/su1.asp?studioid=531524&tg=&vt=&lvl=&stype=&view=&trn=0&page=&catid=&prodid=&date=9%2f28%2f2025&classid=0&prodGroupId=&sSU=&optForwardingLink=&qParam=&justloggedin=&nLgIn=&pMode=0&loc=1",
                new NavigationOptions { WaitUntil = [WaitUntilNavigation.Networkidle0] })
                .ConfigureAwait(false);

            // Wait for login form to load
            await page.WaitForSelectorAsync("#su1UserName").ConfigureAwait(false);
            await page.WaitForSelectorAsync("#su1Password").ConfigureAwait(false);

            // Fill credentials
            await page.TypeAsync("#su1UserName", options.Username, new TypeOptions { Delay = 100 }).ConfigureAwait(false);
            await page.TypeAsync("#su1Password", options.Password, new TypeOptions { Delay = 100 }).ConfigureAwait(false);

            // Submit form and wait for navigation
            await page.ClickAsync("#btnSu1Login").ConfigureAwait(false);
            await page.WaitAsync().ConfigureAwait(false);

            // Verify successful login
            bool isLoggedIn = await page.EvaluateExpressionAsync<bool>(
                "document.querySelector('#myInfoContainer') !== null")
                .ConfigureAwait(false);

            if (!isLoggedIn)
            {
                throw new InvalidOperationException("Unable to login");
            }

            await page.ClickAsync(".tab-c-firstTab > a").ConfigureAwait(false);
            await page.WaitAsync().ConfigureAwait(false);

            if (DateTime.Today.DayOfWeek == DayOfWeek.Saturday
                || DateTime.Today.DayOfWeek == DayOfWeek.Sunday)
            {
                await page.ClickAsync("#day-arrow-r").ConfigureAwait(false);
                await page.WaitAsync().ConfigureAwait(false);
            }

            string script = File.ReadAllText("class_selector.js");

            JsonSerializerOptions opt = new(JsonSerializerDefaults.Web);
            opt.Converters.Add(new DateTimeConverter());

            JsonDocument o = await page.EvaluateFunctionAsync<JsonDocument>(script).ConfigureAwait(false);
            List<ClassScheduleItem> items = JsonSerializer.Deserialize<List<ClassScheduleItem>>(o.RootElement.GetRawText(), opt) ?? [];

            return page;
        }
    }

    internal static class Ext
    {
        public static Task WaitAsync(this IPage page) =>
            page.WaitForNavigationAsync(new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle0],
                Timeout = 30000
            });
    }


    public class ClassScheduleItem
    {
        public string ClassName { get; set; }
        public string Instructor { get; set; }
        public string Room { get; set; }
        public string Duration { get; set; }
        public string Availability { get; set; }
        public DateTime Date { get; set; }
    }

    sealed class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string isoDate = reader.GetString()!;

            DateTimeOffset dto = DateTimeOffset.Parse(isoDate);
            return dto.LocalDateTime;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            value.ToString("o");
    }
}
