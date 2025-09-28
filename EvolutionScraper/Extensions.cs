using PuppeteerSharp;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvolutionScraper
{
    internal static class Extensions
    {
        public static Task WaitAsync(this IPage page) =>
            page.WaitForNavigationAsync(new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle0],
                Timeout = 30000
            });

        public static DateTime GetNextDateTime(DayOfWeek targetDay, TimeOnly time)
        {
            DateTime today = DateTime.Today;
            int daysUntilTarget = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;
            DateTime targetDate = today.AddDays(daysUntilTarget);

            return targetDate.Add(time.ToTimeSpan());
        }
    }

    internal sealed class DateTimeConverter : JsonConverter<DateTime>
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
