using StatusImageCard.Models;

namespace StatusImageCard.Service
{
    /// <summary>Pure whitelist matching and tile ordering.</summary>
    public static class ContainerSelection
    {
        /// <summary>Parses "a, b,c" into trimmed, non-empty entries.</summary>
        public static IReadOnlyList<string> ParseWhitelist(string? raw) =>
            (raw ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>True when the selector equals the container name or is a prefix of its id (case-insensitive).</summary>
        public static bool MatchesSelector(ZcogContainerModel c, string selector) =>
            !string.IsNullOrWhiteSpace(selector) &&
            (c.Name.Equals(selector, StringComparison.OrdinalIgnoreCase) ||
             c.Id.StartsWith(selector, StringComparison.OrdinalIgnoreCase));

        /// <summary>An empty whitelist matches nothing.</summary>
        public static bool IsWhitelisted(ZcogContainerModel c, IReadOnlyCollection<string> whitelist) =>
            whitelist.Any(w => MatchesSelector(c, w));

        public static bool IsOnline(ZcogContainerModel c) =>
            c.State.Equals("running", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whitelisted containers: online first, then offline, alphabetical (case-insensitive) within each group.</summary>
        public static List<ZcogContainerModel> SelectAndOrder(IEnumerable<ZcogContainerModel> all, IReadOnlyCollection<string> whitelist) =>
            all.Where(c => IsWhitelisted(c, whitelist))
               .OrderBy(c => IsOnline(c) ? 0 : 1)
               .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
               .ThenBy(c => c.Id, StringComparer.Ordinal)
               .ToList();

        /// <summary>The first whitelisted container the selector matches, or null. Containers outside the whitelist are never returned.</summary>
        public static ZcogContainerModel? FindWhitelisted(IEnumerable<ZcogContainerModel> all, IReadOnlyCollection<string> whitelist, string selector) =>
            all.FirstOrDefault(c => IsWhitelisted(c, whitelist) && MatchesSelector(c, selector));
    }
}
