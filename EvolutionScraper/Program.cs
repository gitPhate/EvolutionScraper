
using EvolutionScraper;
using System.Text.Json;

EvolutionScraperOptions opt =
    JsonSerializer.Deserialize<EvolutionScraperOptions>(File.ReadAllText(@"appsettings.json"))
    ?? throw new JsonException("Unable to deserialize options");

EvolutionScraper.EvolutionScraper scraper = new(opt);
bool isBooked = await scraper.BookClassAsync("Pilates", DayOfWeek.Monday, new TimeOnly(8, 30));
Console.ReadLine();