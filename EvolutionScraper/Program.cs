
using EvolutionScraper;
using System.Text.Json;

EvolutionScraperOptions opt =
    JsonSerializer.Deserialize<EvolutionScraperOptions>(File.ReadAllText(@"appsettings.json"))
    ?? throw new JsonException("Unable to deserialize options");

EvolutionScraper.EvolutionScraper scraper = new(opt);
ClassScheduleItem[] items = await scraper.GetClassSchedulesAsync();
bool a = true;