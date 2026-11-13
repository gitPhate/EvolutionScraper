using EvolutionScraper;
using EvolutionScraper.Service.Support;
using EvolutionScraper.Service.Support.DI;
using EvolutionScraper.Service.Support.Quartz;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Hosting;
using Quartz;
using System.Reflection;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// Booking opens at 09:05: the scraper logs in and then waits for it, so it must start between 08:55 and 09:00
const string BookingCron = "0 59 8 * * ?";

await Host
    .CreateDefaultBuilder(args)
    .UseWindowsService()
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureServices
    (
        (hostContext, services) =>
        {
            services
                .AddSingletonOption<EvolutionScraperOptions>()
                .AddSingletonOption<QuartzConfig>()
                .AddQuartz
                (opt =>
                {
                    QuartzConfig quartzConfig = hostContext.Configuration.GetRequiredOption<QuartzConfig>();

                    opt.UseSimpleTypeLoader();
                    opt.UseInMemoryStore();
                    opt.UseDefaultThreadPool(tp =>
                    {
                        tp.MaxConcurrency = 10;
                    });

                    Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

                    foreach (QuartzJobSchedule schedule in quartzConfig.Jobs)
                    {
                        if (!schedule.IsActive)
                        {
                            continue;
                        }

                        if (schedule.Name.IsNullOrBlankString())
                        {
                            throw new InvalidOperationException($"A job of type {schedule.FullTypeName} has no {nameof(QuartzJobSchedule.Name)}");
                        }

                        Type? jobType =
                            assembly.GetType(schedule.FullTypeName, false)
                            ?? throw new TypeLoadException($"The job with {nameof(QuartzJobSchedule.FullTypeName)} {schedule.FullTypeName} has not been found in the assembly {assembly.FullName}");

                        string? calendarName = null;

                        if (!schedule.Calendar.IsNullOrBlankString())
                        {
                            if (schedule.Calendar != BookingDayCalendar.Name)
                            {
                                throw new NotSupportedException($"Unknown calendar '{schedule.Calendar}' for job {schedule.Name}");
                            }

                            DayOfWeek bookedDay = BookingJobData.Parse(schedule.Data).DayOfWeek;
                            calendarName = $"{schedule.Name}-{BookingDayCalendar.Name}";
                            opt.AddCalendar<BookingDayCalendar>(calendarName, AddCalendarOptions.Replacing, c => c.BookedDay = bookedDay);
                        }

                        opt
                            .AddJob(jobType, configure: (sp, x) =>
                            {
                                x.WithIdentity(schedule.Name);

                                foreach (KeyValuePair<string, string> item in schedule.Data)
                                {
                                    x.UsingJobData(item.Key, item.Value);
                                }
                            })
                            .AddTrigger(b =>
                            {
                                b
                                    .WithIdentity(schedule.Name)
                                    .ForJob(schedule.Name)
                                    .WithCronSchedule(BookingCron);

                                if (calendarName is not null)
                                {
                                    b.WithCalendarName(calendarName);
                                }

                                if (schedule.StartAfterInSeconds.HasValue)
                                {
                                    b.StartAt(DateTimeOffset.UtcNow.AddSeconds(schedule.StartAfterInSeconds.Value));
                                }
                                else
                                {
                                    b.StartNow();
                                }
                            });

                        // Testing only: fires once at startup, without the calendar, so it books regardless of the day rules
                        if (!schedule.StartAfterInSeconds.HasValue && (schedule.StartImmediately ?? false))
                        {
                            opt.AddTrigger(b => b
                                .WithIdentity($"{schedule.Name}-immediate")
                                .ForJob(schedule.Name)
                                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                                .StartNow());
                        }
                    }
                })
                .AddQuartzHostedService(options =>
                {
                    options.WaitForJobsToComplete = true;
                    options.AwaitApplicationStarted = true;
                });
        }
    )
    .ConfigureLogging
    (
        (context, logging) =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Trace : LogLevel.Debug);
        }
    )
    .UseNLog()
    .Build()
    .RunAsync();
