# EvolutionScraper

Never miss a gym class again. EvolutionScraper is a Windows service that automatically books your favorite Evolution gym classes on a schedule you define.

## What It Does

EvolutionScraper handles the tedious work of securing your spot in popular classes. Simply tell it which classes you want and when — the service logs in, finds your class, and books it automatically, right when bookings open.

## Key Features

- **Scheduled Bookings** — Configure your weekly class schedule once and let it run
- **Smart Timing** — Automatically waits for the exact moment bookings become available
- **Multiple Classes** — Book different classes on different days of the week
- **Headless Operation** — Runs silently in the background with no browser window
- **Detailed Logging** — Full transparency into what the service is doing

## Quick Start

1. **Configure** your classes in `appsettings.json`:
   ```json
   {
     "QuartzConfig": {
       "RetryDelayInSeconds": 60,
       "MaxRetries": 3,
       "Jobs": [
         {
           "Name": "Calisthenics-Wednesday",
           "FullTypeName": "EvolutionScraper.Service.Jobs.ScrapeJob",
           "IsActive": true,
           "Calendar": "BookingDay",
           "Data": { "ClassName": "Calisthenics", "DayOfWeek": "Wednesday", "Time": "20:00:00" }
         }
       ]
     }
   }
   ```
   Each job is one booking. It starts every day at 08:59, but the `BookingDay` calendar lets it fire only on the day booking opens (3 days before the class, or the 1st of the month when that day falls in the previous month). If a booking fails it is retried `MaxRetries` times, `RetryDelayInSeconds` apart.

2. **Install** the Windows service and start it

3. **Relax** — your classes are booked automatically

## Configuration

All settings live in `appsettings.json`:

| Setting | Description |
|---------|-------------|
| `EvolutionScraperOptions:ChromePath` | Path to Chrome browser |
| `EvolutionScraperOptions:Username` | Your Evolution gym email |
| `EvolutionScraperOptions:Password` | Your Evolution gym password |
| `QuartzConfig:Jobs` | One entry per class to book (`Name`, `Calendar`, `Data`, optional `StartAfterInSeconds`) |
| `QuartzConfig:Jobs:StartImmediately` | Testing only: fires once at startup and books regardless of the day rules |
| `QuartzConfig:RetryDelayInSeconds` / `MaxRetries` | Delay and number of retries after a failed booking |

---

*Built with .NET 8, PuppeteerSharp, and Quartz.NET*
