namespace StatusImageCard.Discord;

/// <summary>Builds the embed description text for the status message.</summary>
public static class DiscordDescriptionBuilder
{
    public static string Build(long generatedAtUnix, IReadOnlyCollection<string> online, IReadOnlyCollection<string> offline)
    {
        var lines = new List<string> { $"Generated at <t:{generatedAtUnix}>" };

        if (online.Count > 0)
            lines.Add($"Online: {string.Join(", ", online)}");

        if (offline.Count > 0)
            lines.Add($"Offline: {string.Join(", ", offline)}");

        return string.Join("\n", lines);
    }
}
