# AGENTS.md — EvolutionScraper

Agent-facing guide for the EvolutionScraper repository. Read this before making changes.

---

## Project Overview

**EvolutionScraper** is a .NET 8.0 Windows Service that automatically books gym classes at Evolution gym via browser automation (PuppeteerSharp / Chromium) on a Quartz.NET schedule.

**Solution structure:**
```
EvolutionScraper/           # Class library — core scraper logic
EvolutionScraper.Service/   # Windows Service executable — DI setup, job scheduling
```

---

## Build / Run Commands

```bash
# Restore dependencies
dotnet restore

# Build (debug)
dotnet build

# Build (release)
dotnet build -c Release

# Run the service locally
dotnet run --project EvolutionScraper.Service

# Publish as a self-contained Windows Service
dotnet publish EvolutionScraper.Service -c Release -r win-x64 -o ./publish

# Clean build artifacts
dotnet clean
```

---

## Test Commands

**There are currently no test projects in this solution.**

When adding tests, the expected conventions are:
```bash
# Run all tests
dotnet test

# Run a single test by fully-qualified name
dotnet test --filter "FullyQualifiedName=Namespace.ClassName.MethodName"

# Run tests matching a pattern
dotnet test --filter "DisplayName~BookClass"

# Run tests in a specific project
dotnet test EvolutionScraper.Tests/EvolutionScraper.Tests.csproj
```

Use **xUnit** as the preferred test framework if tests are added; follow the `Namespace.Tests` naming pattern for test projects.

---

## Code Style Guidelines

### Language & Framework
- Target **C# 12** features on **.NET 8.0**.
- Both projects enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
- Treat all nullable warnings as errors in your head — do not suppress them without justification.

### Naming Conventions
| Construct | Convention | Example |
|-----------|-----------|---------|
| Classes, Methods, Properties | PascalCase | `BookClassAsync`, `ClassName` |
| Private / instance fields | `_camelCase` | `_options`, `_browser`, `_page` |
| Local variables, parameters | camelCase | `launchOptions`, `classToBook` |
| Constants | PascalCase | `StartImmediatlyTriggerName` |
| Async methods | Suffix `Async` | `LoginAsync`, `RunBrowserAsync` |

### Class Design
- Prefer **`sealed`** classes to prevent unintended inheritance:
  ```csharp
  public sealed class EvolutionScraper(...) : IDisposable, IAsyncDisposable { }
  internal sealed class DateTimeConverter : JsonConverter<DateTime> { }
  ```
- Use **`record`** for immutable option/configuration types:
  ```csharp
  public record EvolutionScraperOptions(string ChromePath, string Username, string Password)
  {
      public EvolutionScraperOptions() : this(string.Empty, string.Empty, string.Empty) { }
  }
  ```
- Use **primary constructors** (C# 12) for DI-injected classes:
  ```csharp
  internal class ScrapeJob(
      ILogger<ScrapeJob> logger,
      EvolutionScraperOptions options,
      Dictionary<DayOfWeek, ClassBooking[]> bookings)
      : BaseJob(logger)
  ```

### Async/Await
- All I/O-bound methods must be `async Task` or `async ValueTask`; name them with the `Async` suffix.
- Always apply **`ConfigureAwait(false)`** in library (`EvolutionScraper`) code:
  ```csharp
  await RunBrowserAsync().ConfigureAwait(false);
  await LoginAsync().ConfigureAwait(false);
  ```
- Use `ValueTask` for hot-path or lightweight async operations; `Task` everywhere else.
- Never call `.Result` or `.GetAwaiter().GetResult()` except inside `Dispose()` where async is unavailable.

### Null Handling
- Nullable reference types are enabled — never disable them project-wide.
- Use `is null` / `is not null` pattern matching for null checks (not `== null`):
  ```csharp
  if (_page is null)
      throw new InvalidOperationException("Browser is not initialized.");
  ```
- Use the null-coalescing default operator `?? []` or `?? string.Empty` for safe fallbacks.
- Prefer `!` postfix only when you can guarantee non-null via logic that the compiler cannot see.

### Error Handling
- Catch exceptions at job/boundary level; do not swallow them silently.
- Log before re-throwing. In scraper code, dump the current page HTML for diagnostics:
  ```csharp
  private async Task ThrowLoggingPageAsync(Exception ex)
  {
      if (_page is not null)
      {
          string content = await _page.GetContentAsync().ConfigureAwait(false);
          await File.WriteAllTextAsync($"page_dump_{DateTime.Now:yyyyMMddHHmmss}.html", content)
              .ConfigureAwait(false);
      }
      throw ex;
  }
  ```
- `BaseJob.Execute` wraps all job execution in try/catch/finally — always delegate job logic to `ExecuteImplAsync`.

### Disposal
- Classes that own unmanaged or disposable resources implement both `IDisposable` and `IAsyncDisposable`.
- `IAsyncDisposable` is preferred at call sites when available; `IDisposable` exists as a sync fallback.
- Always check `IsClosed` / null before disposing browser/page objects.

### Imports (using directives)
- Rely on **implicit global usings** for common namespaces (`System`, `System.Collections.Generic`, etc.).
- Add explicit `using` statements at the top of each file, grouped as:
  1. `Microsoft.*` / `System.*` (alphabetical)
  2. Third-party packages (`PuppeteerSharp`, `Quartz`, `NLog`, etc.)
  3. Internal project namespaces
- No unused `using` directives.

### Collections & LINQ
- Prefer collection expressions `[item1, item2]` over `new List<T> { }` where possible (C# 12):
  ```csharp
  Args = ["--disable-blink-features=AutomationControlled", "--disable-dev-shm-usage"]
  ```
- Chain LINQ in method syntax; avoid query syntax.
- Use `?? []` as an empty-collection fallback after `Deserialize`.

### String Formatting
- Use **string interpolation** (`$"..."`) for runtime values.
- Use **verbatim strings** (`@"..."`) for multi-line JavaScript or long selectors injected into the page.
- Format DateTime in file names with `DateTime.Now:yyyyMMddHHmmss`.

### Logging
- Use **NLog** via the `Microsoft.Extensions.Logging.ILogger<T>` abstraction; never reference NLog directly in business logic.
- Log at appropriate levels: `LogInformation` for normal flow, `LogWarning` for recoverable issues, `LogError` for exceptions.
- Include structured context (job name, class name, booking time) in log messages.

---

## Dependency Injection

- Register options via the custom `AddSingletonOption<T>` extension in `OptionsDIExtensions.cs`:
  ```csharp
  services.AddSingletonOption<EvolutionScraperOptions>()
          .AddSingletonOption<Dictionary<DayOfWeek, ClassBooking[]>>("Bookings")
  ```
- All DI registrations live in `Program.cs` — do not scatter registrations across the codebase.
- Prefer constructor injection (via primary constructors); avoid service locator patterns.

---

## Configuration

- Application settings live in `EvolutionScraper.Service/appsettings.json`.
- **Do not commit real credentials.** Use .NET User Secrets (`dotnet user-secrets`) for local development and a secure vault for production.
- Quartz job schedules are configured in `appsettings.json` under `QuartzConfig`; supports both simple interval and cron expression triggers.

---

## Project Conventions

- The **`EvolutionScraper`** project is a pure class library — it has no entry point and no direct dependency on hosting abstractions.
- The **`EvolutionScraper.Service`** project owns all hosting, scheduling, and DI wiring.
- JavaScript executed in the browser lives in `EvolutionScraper/class_selector.js` and is loaded as an embedded resource — keep browser automation scripts in `.js` files, not inline C# strings.
- DTOs shared between layers go in `EvolutionScraper/DTOs.cs`; keep them plain data types (`class` or `struct`), no logic.
- Extension methods go in `Extensions.cs` within the relevant project; keep them focused and stateless.
