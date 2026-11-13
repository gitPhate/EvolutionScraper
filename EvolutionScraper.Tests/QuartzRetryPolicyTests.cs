using EvolutionScraper.Service.Support.Quartz;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;

namespace EvolutionScraper.Tests;

public sealed class QuartzRetryPolicyTests
{
    private static int _executions;

    private sealed class FailingJob : BaseJob
    {
        public FailingJob() : base(NullLogger.Instance)
        {
        }

        protected override Task ExecuteImplAsync(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _executions);
            throw new InvalidOperationException("Booking failed");
        }
    }

    // MaxRetries = 2 → the first run plus two retries, then the occurrence is over
    [Fact]
    public async Task FailingJob_IsRetriedMaxRetriesTimes_ThenStops()
    {
        _executions = 0;

        ServiceCollection services = new();
        services.AddQuartz(opt =>
        {
            opt.UseInMemoryStore();
            opt.UseDefaultRetryPolicy(RetryPolicy.Fixed(2, TimeSpan.FromMilliseconds(200)));
            opt.AddJob<FailingJob>(j => j.WithIdentity("failing"))
                .AddTrigger(t => t.WithIdentity("failing").ForJob("failing").StartNow());
        });

        await using ServiceProvider provider = services.BuildServiceProvider();
        IScheduler scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler(TestContext.Current.CancellationToken);

        await scheduler.Start(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(4), TestContext.Current.CancellationToken);
        await scheduler.Shutdown(true, TestContext.Current.CancellationToken);

        Assert.Equal(3, _executions);
    }
}
