using QLStats.Data.Entities;
using Xunit;

namespace QLStats.Tests.Scoring;

public class ScoringRuleTests
{
    private static MatchPlayer MakePlayer(
        int kills = 0, int deaths = 0, int suicides = 0,
        int damageDealt = 0, int damageTaken = 0,
        bool won = false,
        int roundsWon = 0, int roundsLost = 0,
        string team = "FREE",
        Dictionary<string, int>? medals = null,
        string gameType = "CA")
    {
        var match = new Match { GameType = gameType };
        return new MatchPlayer
        {
            Kills = kills,
            Deaths = deaths,
            Suicides = suicides,
            DamageDealt = damageDealt,
            DamageTaken = damageTaken,
            Won = won,
            RoundsWon = roundsWon,
            RoundsLost = roundsLost,
            Team = team,
            Medals = medals ?? new Dictionary<string, int>(),
            Match = match
        };
    }

    private static ScoringRule Rule(ScoringRuleType type, decimal value,
        string? gameTypeFilter = null, string? medalType = null) =>
        new()
        {
            Type = type,
            Value = value,
            GameTypeFilter = gameTypeFilter,
            MedalType = medalType,
            SeasonId = 1,
            Season = new Season { Id = 1, Name = "Test" }
        };

    // ── KillsMultiplier ───────────────────────────────────────────────────────

    [Fact]
    public void KillsMultiplier_ReturnsKillsTimesValue()
    {
        var mp = MakePlayer(kills: 10);
        var rule = Rule(ScoringRuleType.KillsMultiplier, 2m);

        Assert.Equal(20m, rule.Calculate(mp));
    }

    [Fact]
    public void KillsMultiplier_ZeroKills_ReturnsZero()
    {
        var mp = MakePlayer(kills: 0);
        var rule = Rule(ScoringRuleType.KillsMultiplier, 3m);

        Assert.Equal(0m, rule.Calculate(mp));
    }

    // ── SuicidesMultiplier ────────────────────────────────────────────────────

    [Fact]
    public void SuicidesMultiplier_ReturnsSuicidesTimesValue()
    {
        var mp = MakePlayer(suicides: 5);
        var rule = Rule(ScoringRuleType.SuicidesMultiplier, -1m);

        Assert.Equal(-5m, rule.Calculate(mp));
    }

    [Fact]
    public void SuicidesMultiplier_ZeroSuicides_ReturnsZero()
    {
        var mp = MakePlayer(suicides: 0);
        var rule = Rule(ScoringRuleType.SuicidesMultiplier, -2m);

        Assert.Equal(0m, rule.Calculate(mp));
    }

    // ── WinMultiplier ─────────────────────────────────────────────────────────

    [Fact]
    public void WinMultiplier_Won_ReturnsValue()
    {
        var mp = MakePlayer(won: true);
        var rule = Rule(ScoringRuleType.WinMultiplier, 50m);

        Assert.Equal(50m, rule.Calculate(mp));
    }

    [Fact]
    public void WinMultiplier_Lost_ReturnsZero()
    {
        var mp = MakePlayer(won: false);
        var rule = Rule(ScoringRuleType.WinMultiplier, 50m);

        Assert.Equal(0m, rule.Calculate(mp));
    }

    // ── DamageMultiplier ──────────────────────────────────────────────────────

    [Fact]
    public void DamageMultiplier_ReturnsDamageTimesValueFloored()
    {
        var mp = MakePlayer(damageDealt: 3000);
        var rule = Rule(ScoringRuleType.DamageMultiplier, 0.01m);

        Assert.Equal(30m, rule.Calculate(mp));
    }

    [Fact]
    public void DamageMultiplier_LegacyRate_FloorsDivision()
    {
        // floor(299 / 150) = 1, floor(300 / 150) = 2
        var rule = Rule(ScoringRuleType.DamageMultiplier, 1m / 150m);

        Assert.Equal(1m, rule.Calculate(MakePlayer(damageDealt: 299)));
        Assert.Equal(2m, rule.Calculate(MakePlayer(damageDealt: 300)));
        Assert.Equal(0m, rule.Calculate(MakePlayer(damageDealt: 149)));
    }

    // ── MedalMultiplier ───────────────────────────────────────────────────────

    [Fact]
    public void MedalMultiplier_MedalPresent_ReturnsMedalCountTimesValue()
    {
        var mp = MakePlayer(medals: new Dictionary<string, int> { ["Excellent"] = 3 });
        var rule = Rule(ScoringRuleType.MedalMultiplier, 5m, medalType: "Excellent");

        Assert.Equal(15m, rule.Calculate(mp));
    }

    [Fact]
    public void MedalMultiplier_MedalAbsent_ReturnsZero()
    {
        var mp = MakePlayer(medals: new Dictionary<string, int>());
        var rule = Rule(ScoringRuleType.MedalMultiplier, 5m, medalType: "Impressive");

        Assert.Equal(0m, rule.Calculate(mp));
    }

    [Fact]
    public void MedalMultiplier_NullMedalType_SumsAllMedals()
    {
        var mp = MakePlayer(medals: new Dictionary<string, int>
        {
            ["Excellent"] = 2,
            ["Impressive"] = 3,
            ["Headhunter"] = 1
        });
        var rule = Rule(ScoringRuleType.MedalMultiplier, 1m, medalType: null);

        Assert.Equal(6m, rule.Calculate(mp));
    }

    [Fact]
    public void MedalMultiplier_NullMedalType_NoMedals_ReturnsZero()
    {
        var mp = MakePlayer(medals: new Dictionary<string, int>());
        var rule = Rule(ScoringRuleType.MedalMultiplier, 1m, medalType: null);

        Assert.Equal(0m, rule.Calculate(mp));
    }

    // ── GameTypeFilter ────────────────────────────────────────────────────────

    [Fact]
    public void GameTypeFilter_MatchingGameType_AppliesRule()
    {
        var mp = MakePlayer(kills: 10, gameType: "CA");
        var rule = Rule(ScoringRuleType.KillsMultiplier, 2m, gameTypeFilter: "CA");

        Assert.Equal(20m, rule.Calculate(mp));
    }

    [Fact]
    public void GameTypeFilter_NonMatchingGameType_ReturnsZero()
    {
        var mp = MakePlayer(kills: 10, gameType: "Duel");
        var rule = Rule(ScoringRuleType.KillsMultiplier, 2m, gameTypeFilter: "CA");

        Assert.Equal(0m, rule.Calculate(mp));
    }

    [Fact]
    public void NoGameTypeFilter_AppliesToAnyGameType()
    {
        var mp = MakePlayer(kills: 7, gameType: "Duel");
        var rule = Rule(ScoringRuleType.KillsMultiplier, 3m, gameTypeFilter: null);

        Assert.Equal(21m, rule.Calculate(mp));
    }

    // ── RoundsWonMultiplier ───────────────────────────────────────────────────

    [Fact]
    public void RoundsWonMultiplier_ReturnsRoundsWonTimesValue()
    {
        var mp = MakePlayer(roundsWon: 6);
        var rule = Rule(ScoringRuleType.RoundsWonMultiplier, 4m);

        Assert.Equal(24m, rule.Calculate(mp));
    }
}
