using StatusImageCard.Discord;

namespace StatusImageCard.Tests;

public class DiscordDescriptionBuilderTests
{
    [Fact]
    public void Online_and_offline_lines()
    {
        var text = DiscordDescriptionBuilder.Build(1763235737, ["atm11", "minecraft"], ["palworld", "rust"]);
        Assert.Equal("Generated at <t:1763235737>\nOnline: atm11, minecraft\nOffline: palworld, rust", text);
    }

    [Fact]
    public void Empty_offline_list_omits_the_line()
    {
        var text = DiscordDescriptionBuilder.Build(1, ["atm11"], []);
        Assert.Equal("Generated at <t:1>\nOnline: atm11", text);
    }

    [Fact]
    public void Empty_online_list_omits_the_line()
    {
        var text = DiscordDescriptionBuilder.Build(1, [], ["rust"]);
        Assert.Equal("Generated at <t:1>\nOffline: rust", text);
    }

    [Fact]
    public void Both_empty_is_just_the_timestamp()
    {
        Assert.Equal("Generated at <t:1>", DiscordDescriptionBuilder.Build(1, [], []));
    }

    [Fact]
    public void No_currently_showing_marker()
    {
        var text = DiscordDescriptionBuilder.Build(1, ["a", "b"], ["c"]);
        Assert.DoesNotContain("[", text);
    }
}
