using StatusImageCard.Service;

namespace StatusImageCard.Tests;

public class ContainerSelectionTests
{
    [Fact]
    public void ParseWhitelist_trims_and_drops_empty_and_duplicate_entries()
    {
        var list = ContainerSelection.ParseWhitelist(" atm11, ,Valheim,valheim,, rust ");
        Assert.Equal(["atm11", "Valheim", "rust"], list);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void ParseWhitelist_empty(string? raw)
    {
        Assert.Empty(ContainerSelection.ParseWhitelist(raw));
    }

    [Theory]
    [InlineData("valheim", true)]
    [InlineData("VALHEIM", true)]
    [InlineData("00ab5b82", true)]
    [InlineData("00AB5B82", true)]
    [InlineData("valheim2", false)]
    [InlineData("valh", false)]
    [InlineData("ab5b82", false)]
    [InlineData("", false)]
    public void MatchesSelector_by_exact_name_or_id_prefix(string selector, bool expected)
    {
        var c = TestData.Container("valheim", running: true, id: "00ab5b82043bc428942c22a1c589169732206f721750626cbecb25105a7ca015");
        Assert.Equal(expected, ContainerSelection.MatchesSelector(c, selector));
    }

    [Fact]
    public void Empty_whitelist_matches_nothing()
    {
        var c = TestData.Container("valheim", running: true);
        Assert.False(ContainerSelection.IsWhitelisted(c, []));
    }

    [Theory]
    [InlineData("running", true)]
    [InlineData("Running", true)]
    [InlineData("exited", false)]
    [InlineData("restarting", false)]
    [InlineData("", false)]
    public void IsOnline_only_when_running(string state, bool expected)
    {
        var c = TestData.Container("x", running: true);
        c.State = state;
        Assert.Equal(expected, ContainerSelection.IsOnline(c));
    }

    [Fact]
    public void SelectAndOrder_puts_online_first_then_offline_each_alphabetical()
    {
        var all = new[]
        {
            TestData.Container("valheim", running: true),
            TestData.Container("rust", running: false),
            TestData.Container("Zomboid", running: true),
            TestData.Container("project-zomboid", running: false),
            TestData.Container("palworld", running: false),
            TestData.Container("atm11", running: true),
            TestData.Container("minecraft-backups", running: true),
            TestData.Container("minecraft", running: true),
        };
        var whitelist = ContainerSelection.ParseWhitelist("atm11,minecraft,valheim,zomboid,palworld,rust,nosuchserver");

        var ordered = ContainerSelection.SelectAndOrder(all, whitelist).Select(c => c.Name);

        Assert.Equal(["atm11", "minecraft", "valheim", "Zomboid", "palworld", "rust"], ordered);
    }

    [Fact]
    public void SelectAndOrder_matches_by_id_prefix_and_counts_each_container_once()
    {
        var all = new[]
        {
            TestData.Container("alpha", running: true, id: "aaaa1111"),
            TestData.Container("beta", running: false, id: "bbbb2222"),
        };

        var ordered = ContainerSelection.SelectAndOrder(all, ["AAAA", "alpha", "bbbb2"]).Select(c => c.Name);

        Assert.Equal(["alpha", "beta"], ordered);
    }

    [Fact]
    public void FindWhitelisted_matches_by_name_or_id_prefix_case_insensitively()
    {
        var all = new[]
        {
            TestData.Container("alpha", running: true, id: "aaaa1111"),
            TestData.Container("beta", running: false, id: "bbbb2222"),
        };
        string[] whitelist = ["alpha", "beta"];

        Assert.Equal("alpha", ContainerSelection.FindWhitelisted(all, whitelist, "ALPHA")?.Name);
        Assert.Equal("beta", ContainerSelection.FindWhitelisted(all, whitelist, "bbbb")?.Name);
        Assert.Null(ContainerSelection.FindWhitelisted(all, whitelist, "gamma"));
    }

    [Theory]
    [InlineData("arcane")]
    [InlineData("ARCANE")]
    [InlineData("cccc")]
    public void FindWhitelisted_never_returns_a_container_outside_the_whitelist(string selector)
    {
        var all = new[]
        {
            TestData.Container("alpha", running: true, id: "aaaa1111"),
            TestData.Container("arcane", running: true, id: "cccc3333"),
        };

        Assert.Null(ContainerSelection.FindWhitelisted(all, ["alpha"], selector));
    }

    [Fact]
    public void FindWhitelisted_with_an_empty_whitelist_finds_nothing()
    {
        var all = new[] { TestData.Container("alpha", running: true) };
        Assert.Null(ContainerSelection.FindWhitelisted(all, [], "alpha"));
    }
}
