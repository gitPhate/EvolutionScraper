using PuppeteerSharp;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvolutionScraper
{
    internal static class Extensions
    {
        public static Task WaitAsync(this IPage page) =>
            page.WaitForNavigationAsync(new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
                Timeout = 30000
            });

        public static DateTime GetNextDateTime(DayOfWeek targetDay, TimeOnly time)
        {
            DateTime today = DateTime.Today;
            int daysUntilTarget = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;
            DateTime targetDate = today.AddDays(daysUntilTarget);

            return targetDate.Add(time.ToTimeSpan());
        }

        public static string GetURLDate() => DateTime.Today.ToString("M/d/yyyy").Replace("/", "%2f");
    }

    internal sealed class DateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTime.Parse(reader.GetString()!, null, DateTimeStyles.RoundtripKind);

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString("o"));
    }
}
