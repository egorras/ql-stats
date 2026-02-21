using QLStats.Data.Entities;

namespace QLStats.Services;

public class ScoringEngine
{
    public static decimal CalculatePoints(MatchPlayer mp, Season season)
    {
        decimal total = 0;
        foreach (var rule in season.Rules.OrderBy(r => r.SortOrder))
        {
            total += rule.Calculate(mp);
        }
        return total;
    }

    public static (decimal total, Dictionary<string, decimal> breakdown) CalculatePointsWithBreakdown(MatchPlayer mp, Season season)
    {
        var breakdown = new Dictionary<string, decimal>();
        decimal total = 0;

        var orderedRules = season.Rules.OrderBy(r => r.SortOrder).ToList();
        foreach (var rule in orderedRules)
        {
            var points = rule.Calculate(mp);
            total += points;
            if (points != 0)
            {
                var name = rule.GetRuleDescription();

                if (breakdown.ContainsKey(name))
                    breakdown[name] += points;
                else
                    breakdown[name] = points;
            }
        }

        return (total, breakdown);
    }
}
