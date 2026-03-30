using EvolutionScraper;
using EvolutionScraper.Service.Support.DI;
using EvolutionScraper.Service.Support.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Hosting;
using Quartz;
using System.Reflection;

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

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
                .AddSingletonOption<Dictionary<DayOfWeek, ClassBooking[]>>("Bookings")
                .Configure<QuartzConfig>(hostContext.Configuration.GetSection($"{nameof(QuartzConfig)}"))
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

                        Type? jobType =
                            assembly.GetType(schedule.FullTypeName, false)
                            ?? throw new TypeLoadException($"The job with {nameof(QuartzJobSchedule.FullTypeName)} {schedule.FullTypeName} has not been found in the assembly {assembly.FullName}");

                        opt
                            .AddJob(jobType, configure: (sp, x) => x.WithIdentity(schedule.FullTypeName))
                            .AddTrigger(b =>
                            {
                                b
                                    .WithIdentity(schedule.FullTypeName)
                                    .ForJob(schedule.FullTypeName);

                                switch (schedule.ScheduleType)
                                {
                                    case JobScheduleType.Simple:
                                        b
                                            .WithSimpleSchedule(x =>
                                                x.WithIntervalInSeconds(schedule.SimpleValueInSeconds ?? 0)
                                                .RepeatForever()
                                            );
                                        break;
                                    case JobScheduleType.Cron:
                                        b.WithCronSchedule(schedule.CronValue ?? string.Empty);
                                        break;
                                }

                                if (schedule.StartAfterInSeconds.HasValue)
                                {
                                    b.StartAt(DateTimeOffset.UtcNow.AddSeconds(schedule.StartAfterInSeconds.Value));
                                }
                                else if (schedule.StartImmediatly ?? false)
                                {
                                    opt.AddTrigger(bb =>
                                    {
                                        bb
                                        .WithIdentity(string.Format(QuartzConsts.StartImmediatlyTriggerName, schedule.FullTypeName))
                                        .ForJob(schedule.FullTypeName)
                                        .WithSimpleSchedule(x => x.WithRepeatCount(0))
                                        .StartNow();
                                    });
                                }
                                else
                                {
                                    b.StartNow();
                                }
                            });
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