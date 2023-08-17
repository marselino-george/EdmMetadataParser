using System;

namespace EdmMetadataParser.Extensions
{
    public static class StringExtensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            return source?.IndexOf(toCheck, comparison) >= 0;
        }
    }
}
