using QLStats.Data.Entities;
using QLStats.Services;
using Xunit;

namespace QLStats.Tests.Scoring;

public class ScoringEngineTests
{
    private static MatchPlayer MakePlayer(
        int kills = 0, int deaths = 0, int damageDealt = 0,
        bool won = false, string gameType = "CA",
        Dictionary<string, int>? medals = null)
    {
        var match = new Match { GameType = gameType };
        return new MatchPlayer
        {
            Kills = kills,
            Deaths = deaths,
            DamageDealt = damageDealt,
            Won = won,
            Medals = medals ?? new Dictionary<string, int>(),
            Match = match
        };
    }

    private static Season MakeSeason(params ScoringRule[] rules)
    {
        var season = new Season { Id = 1, Name = "Test" };
        int order = 0;
        foreach (var r in rules)
        {
            r.SeasonId = season.Id;
            r.Season = season;
            r.SortOrder = order++;
        }
        season.Rules = rules.ToList();
        return season;
    }

    private static ScoringRule Rule(ScoringRuleType type, decimal value,
        decimal? threshold = null, string? gameTypeFilter = null, string? medalType = null) =>
        new()
        {
            Type = type,
            Value = value,
            Threshold = threshold,
            GameTypeFilter = gameTypeFilter,
            MedalType = medalType
        };

    // ── CalculatePoints ───────────────────────────────────────────────────────

    [Fact]
    public void CalculatePoints_SumsMultipleRulesCorrectly()
    {
        var mp = MakePlayer(kills: 10, deaths: 4, won: true);
        var season = MakeSeason(
            Rule(ScoringRuleType.KillsMultiplier, 2m),   // 20
            Rule(ScoringRuleType.DeathsMultiplier, -1m), // -4
            Rule(ScoringRuleType.WinsBonus, 50m)          // 50
        );

        var total = ScoringEngine.CalculatePoints(mp, season);

        Assert.Equal(66m, total);
    }

    [Fact]
    public void CalculatePoints_NoRules_ReturnsZero()
    {
        var mp = MakePlayer(kills: 99);
        var season = MakeSeason();

        Assert.Equal(0m, ScoringEngine.CalculatePoints(mp, season));
    }

    [Fact]
    public void CalculatePoints_RespectsRuleOrder()
    {
        // All rules produce the same value; test that total = sum of all
        var mp = MakePlayer(kills: 5);
        var season = MakeSeason(
            Rule(ScoringRuleType.KillsMultiplier, 1m),
            Rule(ScoringRuleType.KillsMultiplier, 2m),
            Rule(ScoringRuleType.KillsMultiplier, 3m)
        );

        // 5*1 + 5*2 + 5*3 = 30
        Assert.Equal(30m, ScoringEngine.CalculatePoints(mp, season));
    }

    // ── CalculatePointsWithBreakdown ──────────────────────────────────────────

    [Fact]
    public void CalculatePointsWithBreakdown_ReturnsTotalAndBreakdown()
    {
        var mp = MakePlayer(kills: 8, won: true);
        var season = MakeSeason(
            Rule(ScoringRuleType.KillsMultiplier, 2m),
            Rule(ScoringRuleType.WinsBonus, 50m)
        );

        var (total, breakdown) = ScoringEngine.CalculatePointsWithBreakdown(mp, season);

        Assert.Equal(66m, total);
        Assert.Equal(2, breakdown.Count);
    }

    [Fact]
    public void CalculatePointsWithBreakdown_ZeroPointRules_ExcludedFromBreakdown()
    {
        var mp = MakePlayer(kills: 5, won: false);
        var season = MakeSeason(
            Rule(ScoringRuleType.KillsMultiplier, 2m),  // 10 — included
            Rule(ScoringRuleType.WinsBonus, 50m)          // 0  — excluded
        );

        var (total, breakdown) = ScoringEngine.CalculatePointsWithBreakdown(mp, season);

        Assert.Equal(10m, total);
        Assert.Single(breakdown);
        Assert.DoesNotContain("Win Bonus", breakdown.Keys.First());
    }

    [Fact]
    public void CalculatePointsWithBreakdown_DuplicateDescriptions_MergedBySumming()
    {
        // Two identical KillsMultiplier rules with same value → same description key
        var mp = MakePlayer(kills: 4);
        var ruleA = Rule(ScoringRuleType.KillsMultiplier, 1m);
        var ruleB = Rule(ScoringRuleType.KillsMultiplier, 1m);
        var season = MakeSeason(ruleA, ruleB);

        var (total, breakdown) = ScoringEngine.CalculatePointsWithBreakdown(mp, season);

        // 4*1 + 4*1 = 8
        Assert.Equal(8m, total);
        // Both rules share the same description → merged into one key
        Assert.Single(breakdown);
        Assert.Equal(8m, breakdown.Values.First());
    }

    [Fact]
    public void CalculatePointsWithBreakdown_AllZero_EmptyBreakdown()
    {
        var mp = MakePlayer(kills: 0, won: false);
        var season = MakeSeason(
            Rule(ScoringRuleType.KillsMultiplier, 2m),
            Rule(ScoringRuleType.WinsBonus, 50m)
        );

        var (total, breakdown) = ScoringEngine.CalculatePointsWithBreakdown(mp, season);

        Assert.Equal(0m, total);
        Assert.Empty(breakdown);
    }

    [Fact]
    public void CalculatePointsWithBreakdown_BreakdownTotalMatchesCalculatePoints()
    {
        var mp = MakePlayer(kills: 6, deaths: 2, damageDealt: 3000, won: true);
        var season = MakeSeason(
            Rule(ScoringRuleType.KillsMultiplier, 2m),
            Rule(ScoringRuleType.DeathsMultiplier, -1m),
            Rule(ScoringRuleType.WinsBonus, 100m),
            Rule(ScoringRuleType.DamageThresholdBonus, 25m, threshold: 2000m)
        );

        var simple = ScoringEngine.CalculatePoints(mp, season);
        var (withBreakdown, _) = ScoringEngine.CalculatePointsWithBreakdown(mp, season);

        Assert.Equal(simple, withBreakdown);
    }
}
