using System.Globalization;

namespace StatusImageCard.Helper
{
    public class ContainerTextHelper
    {
        public static DateTimeOffset ParseZcogTimestamp(string ts)
        {
            // Example: "2025-12-15T15:34:08" (no timezone)
            // Treat as UTC to keep your charts stable.
            if (DateTime.TryParse(ts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            {
                if (dt.Kind == DateTimeKind.Unspecified) dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return new DateTimeOffset(dt);
            }

            return DateTimeOffset.UtcNow;
        }

        public static string? ExtractUpPrefix(string status)
        {
            // "Up 26 hours (healthy)" -> "Up 26 hours"
            int idx = status.IndexOf("(", StringComparison.Ordinal);
            return idx > 0 ? status[..idx].Trim() : status;
        }

        public static string? ExtractHealthSuffix(string status)
        {
            // "Up 26 hours (healthy)" -> "healthy"
            int a = status.IndexOf("(", StringComparison.Ordinal);
            int b = status.IndexOf(")", StringComparison.Ordinal);
            if (a >= 0 && b > a) return status[(a + 1)..b].Trim();
            return null;
        }
    }
}
