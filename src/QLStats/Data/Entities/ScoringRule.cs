namespace QLStats.Data.Entities;

public enum ScoringRuleType
{
    KillsMultiplier = 0,
    DeathsMultiplier = 1,
    DamageMultiplier = 2,
    WinsBonus = 3,
    LossesBonus = 4,
    RoundsWonMultiplier = 5,
    RoundsLostMultiplier = 6,
    DamageThresholdBonus = 7,
    KillsThresholdBonus = 8
}

public class ScoringRule
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public ScoringRuleType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? Threshold { get; set; }
    public string? GameTypeFilter { get; set; } // e.g. "CA", "Duel"
    public int SortOrder { get; set; }

    public decimal Calculate(MatchPlayer mp)
    {
        if (!string.IsNullOrEmpty(GameTypeFilter) && mp.Match.GameType != GameTypeFilter)
            return 0;

        return Type switch
        {
            ScoringRuleType.KillsMultiplier => mp.Kills * Value,
            ScoringRuleType.DeathsMultiplier => mp.Deaths * Value,
            ScoringRuleType.DamageMultiplier => mp.DamageDealt * Value,
            ScoringRuleType.WinsBonus => mp.Won ? Value : 0,
            ScoringRuleType.LossesBonus => !mp.Won ? Value : 0,
            ScoringRuleType.RoundsWonMultiplier => mp.RoundsWon * Value,
            ScoringRuleType.RoundsLostMultiplier => mp.RoundsLost * Value,
            ScoringRuleType.DamageThresholdBonus => mp.DamageDealt >= (Threshold ?? 0) ? Value : 0,
            ScoringRuleType.KillsThresholdBonus => mp.Kills >= (Threshold ?? 0) ? Value : 0,
            _ => 0
        };
    }

    public string GetRuleDescription()
    {
        var filter = string.IsNullOrEmpty(GameTypeFilter) ? "" : $" ({GameTypeFilter})";
        return Type switch
        {
            ScoringRuleType.KillsMultiplier => $"Kills x {Value:0.####}{filter}",
            ScoringRuleType.DeathsMultiplier => $"Deaths x {Value:0.####}{filter}",
            ScoringRuleType.DamageMultiplier => $"Damage x {Value:0.######}{filter}",
            ScoringRuleType.WinsBonus => $"Win Bonus {Value:0.####}{filter}",
            ScoringRuleType.LossesBonus => $"Loss Bonus {Value:0.####}{filter}",
            ScoringRuleType.RoundsWonMultiplier => $"Rounds Won x {Value:0.####}{filter}",
            ScoringRuleType.RoundsLostMultiplier => $"Rounds Lost x {Value:0.####}{filter}",
            ScoringRuleType.DamageThresholdBonus => $"Damage >= {Threshold} Bonus {Value:0.####}{filter}",
            ScoringRuleType.KillsThresholdBonus => $"Kills >= {Threshold} Bonus {Value:0.####}{filter}",
            _ => $"{Type}{filter}"
        };
    }
}
