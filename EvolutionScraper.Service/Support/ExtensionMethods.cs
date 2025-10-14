using System.Runtime.CompilerServices;

namespace EvolutionScraper.Service.Support
{
    internal static class ExtensionMethods
    {
        internal static bool IsNullOrBlankString(this string? s) => string.IsNullOrWhiteSpace(s);
        internal static string? ToNullIfBlank(this string? s) => s.IsNullOrBlankString() ? null : s;

        internal static T GetNonNullOrThrow<T>(this T? item, [CallerMemberName] string methodName = "")
        {
            if (item != null)
            {
                return item;
            }

            throw new ArgumentNullException(methodName);
        }
    }
}
