namespace QLStats.Data.Entities;

public enum ScoringRuleType
{
    KillsMultiplier    = 0,
    SuicidesMultiplier = 1,   // was DeathsMultiplier — repurposed, same DB integer
    DamageMultiplier   = 2,
    WinMultiplier      = 3,   // was WinsBonus — flat points per win
    // 4 (LossesBonus), 6 (RoundsLostMultiplier), 7 (DamageThresholdBonus), 8 (KillsThresholdBonus) retired
    RoundsWonMultiplier = 5,
    MedalMultiplier    = 9
}

public class ScoringRule
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;

    public ScoringRuleType Type { get; set; }
    public decimal Value { get; set; }
    public decimal? Threshold { get; set; }     // kept in DB schema; no longer exposed in UI
    public string? GameTypeFilter { get; set; }
    public string? MedalType { get; set; }
    public int SortOrder { get; set; }

    public decimal Calculate(MatchPlayer mp)
    {
        if (!string.IsNullOrEmpty(GameTypeFilter) && mp.Match.GameType != GameTypeFilter)
            return 0;

        return Type switch
        {
            ScoringRuleType.KillsMultiplier     => mp.Kills * Value,
            ScoringRuleType.SuicidesMultiplier  => mp.Suicides * Value,
            ScoringRuleType.DamageMultiplier    => Math.Floor(mp.DamageDealt * Value),
            ScoringRuleType.WinMultiplier       => mp.Won ? Value : 0,
            ScoringRuleType.RoundsWonMultiplier => mp.RoundsWon * Value,
            ScoringRuleType.MedalMultiplier     => MedalType is null
                                                    ? mp.Medals.Values.Sum() * Value
                                                    : mp.Medals.GetValueOrDefault(MedalType, 0) * Value,
            _                                   => 0
        };
    }

    public string GetRuleDescription()
    {
        var filter = string.IsNullOrEmpty(GameTypeFilter) ? "" : $" ({GameTypeFilter})";
        return Type switch
        {
            ScoringRuleType.KillsMultiplier     => $"Kills x {Value:0.####}{filter}",
            ScoringRuleType.SuicidesMultiplier  => $"Suicides x {Value:0.####}{filter}",
            ScoringRuleType.DamageMultiplier    => $"Damage x {Value:0.######}{filter}",
            ScoringRuleType.WinMultiplier       => $"Win Bonus {Value:0.####}{filter}",
            ScoringRuleType.RoundsWonMultiplier => $"Rounds Won x {Value:0.####}{filter}",
            ScoringRuleType.MedalMultiplier     => MedalType is null
                                                    ? $"All Medals x {Value:0.####}{filter}"
                                                    : $"{MedalType} x {Value:0.####}{filter}",
            _                                   => $"{Type}{filter}"
        };
    }
}
