#nullable disable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvolutionScraper
{
    internal class Settings
    {
        private const string SettingsFilePath = "appsettings.json";

        private static readonly JsonSerializerOptions _jsonSerializerOptions = GetJsonSerializerOptions();

        [JsonRequired]
        public EvolutionScraperOptions EvolutionScraperOptions { get; set; }

        [JsonRequired]
        public Dictionary<DayOfWeek, ClassBooking[]> Bookings { get; set; }

        public static Settings NewFromFile(string filePath = null)
        {
            string settingsFilePath = filePath ?? SettingsFilePath;

            if (!File.Exists(settingsFilePath))
            {
                throw new FileNotFoundException($"Missing config file {settingsFilePath}");
            }

            string settingsFileContent = File.ReadAllText(settingsFilePath);
            Settings settings = JsonSerializer.Deserialize<Settings>(settingsFileContent, _jsonSerializerOptions)
                ?? throw new InvalidOperationException($"Unable to deserialize config file {settingsFilePath}");

            return settings;
        }

        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            JsonSerializerOptions jsonSerializerOptions = new();
            jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<DayOfWeek>());
            return jsonSerializerOptions;
        }
    }

    public class ClassBooking
    {
        public string Name { get; set; }
        public TimeOnly Time { get; set; }
    }
}
