using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EvolutionScraper.Service.Support.DI
{
    public static class OptionsDIExtensions
    {
        public static T? GetOption<T>(this IConfiguration configuration, string? sectionName = null) =>
            configuration
                .GetSection(sectionName.ToNullIfBlank() ?? typeof(T).Name)
                .Get<T>();

        public static T? GetOption<T>(this IServiceProvider provider, string? sectionName = null) =>
            provider
                .GetService<IConfiguration>()!
                .GetOption<T>(sectionName);

        public static T GetRequiredOption<T>(this IConfiguration configuration, string? sectionName = null) =>
            configuration
                .GetOption<T>(sectionName)
                .GetNonNullOrThrow(sectionName.ToNullIfBlank() ?? typeof(T).Name);

        public static T GetRequiredOption<T>(this IServiceProvider provider, string? sectionName = null) =>
            provider
                .GetOption<T>(sectionName)
                .GetNonNullOrThrow(sectionName.ToNullIfBlank() ?? typeof(T).Name);

        public static IServiceCollection AddSingletonOption<T>(this IServiceCollection services, string? sectionName = null) where T : class =>
            services.AddSingleton(provider => provider.GetRequiredOption<T>(sectionName));

        public static IServiceCollection AddSingletonOption<TService, TImplementation>(this IServiceCollection services, string? sectionName = null)
            where TService : class
            where TImplementation : class, TService =>
            services
                .AddSingleton(provider => provider.GetRequiredOption<TImplementation>(sectionName));
    }
}
