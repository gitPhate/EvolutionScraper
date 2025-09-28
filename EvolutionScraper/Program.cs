using EvolutionAutomation;
using PuppeteerSharp;
using System.Text.Json;

EvolutionScraperOptions opt =
    JsonSerializer.Deserialize<EvolutionScraperOptions>(File.ReadAllText(@"appsettings.json"))
    ?? throw new JsonException("Unable to deserialize options");
EvolutionScraper scraper = new(opt);
IPage page = await scraper.GetMainPageAsync();
