using QLStats.Data.Entities;
using Xunit;

namespace QLStats.Tests.Scoring;

public class ScoringRuleTests
{
    // Helper: build a MatchPlayer with required navigation property
    private static MatchPlayer MakePlayer(
        int kills = 0, int deaths = 0,
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
        decimal? threshold = null, string? gameTypeFilter = null, string? medalType = null) =>
        new()
        {
            Type = type,
            Value = value,
            Threshold = threshold,
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

    // ── DeathsMultiplier ──────────────────────────────────────────────────────

    [Fact]
    public void DeathsMultiplier_ReturnsDeathsTimesValue()
    {
        var mp = MakePlayer(deaths: 5);
        var rule = Rule(ScoringRuleType.DeathsMultiplier, -1m);

        Assert.Equal(-5m, rule.Calculate(mp));
    }

    // ── WinsBonus ─────────────────────────────────────────────────────────────

    [Fact]
    public void WinsBonus_Won_ReturnsValue()
    {
        var mp = MakePlayer(won: true);
        var rule = Rule(ScoringRuleType.WinsBonus, 50m);

        Assert.Equal(50m, rule.Calculate(mp));
    }

    [Fact]
    public void WinsBonus_Lost_ReturnsZero()
    {
        var mp = MakePlayer(won: false);
        var rule = Rule(ScoringRuleType.WinsBonus, 50m);

        Assert.Equal(0m, rule.Calculate(mp));
    }

    // ── LossesBonus ───────────────────────────────────────────────────────────

    [Fact]
    public void LossesBonus_Lost_ReturnsValue()
    {
        var mp = MakePlayer(won: false);
        var rule = Rule(ScoringRuleType.LossesBonus, 10m);

        Assert.Equal(10m, rule.Calculate(mp));
    }

    [Fact]
    public void LossesBonus_Won_ReturnsZero()
    {
        var mp = MakePlayer(won: true);
        var rule = Rule(ScoringRuleType.LossesBonus, 10m);

        Assert.Equal(0m, rule.Calculate(mp));
    }

    // ── DamageThresholdBonus ──────────────────────────────────────────────────

    [Fact]
    public void DamageThresholdBonus_Above_ReturnsValue()
    {
        var mp = MakePlayer(damageDealt: 5000);
        var rule = Rule(ScoringRuleType.DamageThresholdBonus, 25m, threshold: 4000m);

        Assert.Equal(25m, rule.Calculate(mp));
    }

    [Fact]
    public void DamageThresholdBonus_Equal_ReturnsValue()
    {
        var mp = MakePlayer(damageDealt: 4000);
        var rule = Rule(ScoringRuleType.DamageThresholdBonus, 25m, threshold: 4000m);

        Assert.Equal(25m, rule.Calculate(mp));
    }

    [Fact]
    public void DamageThresholdBonus_Below_ReturnsZero()
    {
        var mp = MakePlayer(damageDealt: 999);
        var rule = Rule(ScoringRuleType.DamageThresholdBonus, 25m, threshold: 4000m);

        Assert.Equal(0m, rule.Calculate(mp));
    }

    // ── KillsThresholdBonus ───────────────────────────────────────────────────

    [Fact]
    public void KillsThresholdBonus_Above_ReturnsValue()
    {
        var mp = MakePlayer(kills: 20);
        var rule = Rule(ScoringRuleType.KillsThresholdBonus, 30m, threshold: 15m);

        Assert.Equal(30m, rule.Calculate(mp));
    }

    [Fact]
    public void KillsThresholdBonus_Below_ReturnsZero()
    {
        var mp = MakePlayer(kills: 5);
        var rule = Rule(ScoringRuleType.KillsThresholdBonus, 30m, threshold: 15m);

        Assert.Equal(0m, rule.Calculate(mp));
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

    // ── RoundsWon / RoundsLost multipliers ───────────────────────────────────

    [Fact]
    public void RoundsWonMultiplier_ReturnsRoundsWonTimesValue()
    {
        var mp = MakePlayer(roundsWon: 6);
        var rule = Rule(ScoringRuleType.RoundsWonMultiplier, 4m);

        Assert.Equal(24m, rule.Calculate(mp));
    }

    [Fact]
    public void RoundsLostMultiplier_ReturnsRoundsLostTimesValue()
    {
        var mp = MakePlayer(roundsLost: 3);
        var rule = Rule(ScoringRuleType.RoundsLostMultiplier, -2m);

        Assert.Equal(-6m, rule.Calculate(mp));
    }
}
