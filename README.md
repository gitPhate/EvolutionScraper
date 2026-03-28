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
     "Bookings": {
       "Wednesday": [{ "Name": "Calisthenics", "Time": "20:00:00" }],
       "Friday": [{ "Name": "Calisthenics", "Time": "20:00:00" }]
     }
   }
   ```

2. **Install** the Windows service and start it

3. **Relax** — your classes are booked automatically

## Configuration

All settings live in `appsettings.json`:

| Setting | Description |
|---------|-------------|
| `EvolutionScraperOptions:ChromePath` | Path to Chrome browser |
| `EvolutionScraperOptions:Username` | Your Evolution gym email |
| `EvolutionScraperOptions:Password` | Your Evolution gym password |
| `Bookings` | Weekly schedule of classes to book |
| `QuartzConfig` | How often to check and run bookings |

---

*Built with .NET 8, PuppeteerSharp, and Quartz.NET*
